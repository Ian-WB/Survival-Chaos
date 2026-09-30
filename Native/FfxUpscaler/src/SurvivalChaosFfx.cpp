// Survival Chaos - AMD FSR upscaling through AMD's signed FidelityFX DLLs.
//
// HDRP hands the game's Fsr3Upscaler (C#) its inputs through the upscaler
// framework. That class cannot touch D3D12 itself, so it queues a render event
// with a DispatchParams block, and this plugin runs the upscale on Unity's
// render thread, inside the command list Unity is recording.
//
// Textures reach the plugin the way Unity's own FSR2 module sends them: one
// texture-update event per texture, which hands the plugin Unity's own texture
// ID on the thread that runs the commands. A render buffer pointer read on the
// main thread does not survive graphics jobs - resolving one crashed a build on
// 29 September - and GetNativeTexturePtr waits for the render thread.
//
// The part that matters most is resource state. Unity tracks the D3D12 state of
// every texture it owns, and FSR needs its inputs readable by compute and its
// output writable. So before the dispatch Unity is asked to put each texture in
// that state (it adds the barriers to its own list), and afterwards it is told
// what state they were left in. Getting either side wrong is a removed device,
// not a wrong picture.
//
// Contexts are never destroyed while the GPU may still be using them: a retired
// context waits for Unity's frame fence to pass the frame it was retired in.
//
// Built by build.cmd. AMD's headers under ffx/ are MIT, see ffx/license.md.

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d12.h>
#include <dxgi1_5.h> // IUnityGraphicsD3D12.h names IDXGISwapChain without including it

#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"
#include "IUnityLog.h"
#include "IUnityRenderingExtensions.h"

#ifndef _WINDOWS
#define _WINDOWS
#endif
#include "../ffx/api/include/ffx_api.h"
#include "../ffx/api/include/ffx_api_types.h"
#include "../ffx/api/include/ffx_api_loader.h"
#include "../ffx/api/include/dx12/ffx_api_dx12.h"
#include "../ffx/upscalers/include/ffx_upscale.h"

namespace
{
    // Must match FfxNative.cs.
    enum Status : int32_t
    {
        StatusReady = 0,
        StatusNotD3D12 = 1,
        StatusLoaderMissing = 2,
        StatusUpscalerMissing = 3,
        StatusEntryPointsMissing = 4,
        StatusNoUnityInterface = 5,
    };

    enum EventId : int32_t
    {
        // Small numbers, as in Unity's own plugin sample: ConfigureEvent keeps
        // a setting per id and does not say how large an id it accepts.
        EventDispatch = 91,
        EventRelease = 92,
    };

    // Which texture a texture event carries, in the low four bits of its user
    // data; the context id sits above them. Must match FfxNative.cs.
    enum TextureSlot : uint32_t
    {
        TextureColor = 0,
        TextureDepth = 1,
        TextureMotion = 2,
        TextureOutput = 3,
        // Optional: where transparent effects drew, so FSR leans on the current
        // frame there instead of history (see Fsr3ReactiveMask.cs).
        TextureReactive = 4,
        TextureCount = 5,
    };

    // Must match FfxNative.DispatchParams, field for field.
    struct DispatchParams
    {
        int32_t contextId;
        uint32_t createFlags;
        uint32_t renderWidth;
        uint32_t renderHeight;
        uint32_t maxRenderWidth;
        uint32_t maxRenderHeight;
        uint32_t displayWidth;
        uint32_t displayHeight;
        float jitterX;
        float jitterY;
        float motionScaleX;
        float motionScaleY;
        float sharpness;
        int32_t sharpen;
        float frameTimeDeltaMs;
        float preExposure;
        float cameraNear;
        float cameraFar;
        float fovVerticalRadians;
        int32_t reset;
        int32_t debugView;
    };

    constexpr int MaxContexts = 16;
    constexpr int MaxMessages = 40;

    struct Slot
    {
        bool reserved = false;
        ffxContext context = nullptr;
        uint32_t maxRenderWidth = 0;
        uint32_t maxRenderHeight = 0;
        uint32_t displayWidth = 0;
        uint32_t displayHeight = 0;
        uint32_t createFlags = 0;
        uint64_t versionId = 0;
        char versionName[64] = {};
        int32_t lastResult = 0;
        int32_t dispatches = 0;

        // Filled by this frame's texture events, emptied by its dispatch.
        ID3D12Resource* textures[TextureCount] = {};
    };

    struct Retired
    {
        ffxContext context;
        UINT64 fence;
    };

    IUnityInterfaces* s_Unity = nullptr;
    IUnityGraphics* s_Graphics = nullptr;
    IUnityGraphicsD3D12v8* s_D3D12 = nullptr;
    IUnityLog* s_Log = nullptr;

    HMODULE s_LoaderModule = nullptr;
    HMODULE s_UpscalerModule = nullptr;
    ffxFunctions s_Ffx = {};
    int32_t s_LoadStatus = -1;

    std::mutex s_Lock;
    Slot s_Slots[MaxContexts];
    std::vector<Retired> s_Retired;
    int s_MessagesLogged = 0;

    void Log(UnityLogType type, const char* message)
    {
        if (s_Log != nullptr)
        {
            s_Log->Log(type, message, __FILE__, __LINE__);
        }
    }

    void Logf(UnityLogType type, const char* format, ...)
    {
        char buffer[512];
        va_list args;
        va_start(args, format);
        vsnprintf(buffer, sizeof(buffer), format, args);
        va_end(args);
        Log(type, buffer);
    }

    // FSR's own warnings and errors, capped so a per-frame complaint cannot
    // flood the console.
    void OnFfxMessage(uint32_t type, const wchar_t* message)
    {
        if (s_MessagesLogged >= MaxMessages || message == nullptr)
        {
            return;
        }
        s_MessagesLogged++;

        char utf8[512];
        int written = WideCharToMultiByte(CP_UTF8, 0, message, -1, utf8, sizeof(utf8) - 1, nullptr, nullptr);
        utf8[written > 0 ? written : 0] = 0;
        Logf(type == FFX_API_MESSAGE_TYPE_ERROR ? kUnityLogTypeError : kUnityLogTypeWarning, "FSR: %s", utf8);
    }

    std::wstring ModuleDirectory()
    {
        HMODULE self = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCWSTR>(&ModuleDirectory), &self);
        wchar_t path[MAX_PATH];
        DWORD length = GetModuleFileNameW(self, path, MAX_PATH);
        std::wstring result(path, length);
        size_t slash = result.find_last_of(L"\\/");
        return slash == std::wstring::npos ? L"" : result.substr(0, slash + 1);
    }

    // AMD's DLLs sit beside this one. The upscaler is loaded first, by full
    // path, so the loader finds it already in the process whatever search path
    // it uses.
    int32_t LoadFfx()
    {
        if (s_LoadStatus >= 0)
        {
            return s_LoadStatus;
        }

        std::wstring directory = ModuleDirectory();
        s_UpscalerModule = LoadLibraryW((directory + L"amd_fidelityfx_upscaler_dx12.dll").c_str());
        if (s_UpscalerModule == nullptr)
        {
            return s_LoadStatus = StatusUpscalerMissing;
        }

        s_LoaderModule = LoadLibraryW((directory + L"amd_fidelityfx_loader_dx12.dll").c_str());
        if (s_LoaderModule == nullptr)
        {
            return s_LoadStatus = StatusLoaderMissing;
        }

        ffxLoadFunctions(&s_Ffx, s_LoaderModule);
        if (s_Ffx.CreateContext == nullptr || s_Ffx.DestroyContext == nullptr ||
            s_Ffx.Dispatch == nullptr || s_Ffx.Query == nullptr)
        {
            return s_LoadStatus = StatusEntryPointsMissing;
        }

        return s_LoadStatus = StatusReady;
    }

    // Destroys retired contexts once the GPU has finished the frame they were
    // retired in. With force, the caller vouches that the GPU is idle.
    void ReleaseRetired(bool force)
    {
        if (s_Retired.empty())
        {
            return;
        }

        UINT64 completed = UINT64_MAX;
        if (!force)
        {
            ID3D12Fence* fence = s_D3D12 != nullptr ? s_D3D12->GetFrameFence() : nullptr;
            completed = fence != nullptr ? fence->GetCompletedValue() : 0;
        }

        for (size_t i = 0; i < s_Retired.size();)
        {
            if (force || completed >= s_Retired[i].fence)
            {
                s_Ffx.DestroyContext(&s_Retired[i].context, nullptr);
                s_Retired[i] = s_Retired.back();
                s_Retired.pop_back();
            }
            else
            {
                i++;
            }
        }
    }

    void Retire(Slot& slot)
    {
        if (slot.context == nullptr)
        {
            return;
        }

        UINT64 fence = s_D3D12 != nullptr ? s_D3D12->GetNextFrameFenceValue() : 0;
        s_Retired.push_back({slot.context, fence});
        slot.context = nullptr;
        slot.versionName[0] = 0;
        slot.versionId = 0;
    }

    bool Create(Slot& slot, const DispatchParams& p)
    {
        ffxCreateBackendDX12Desc backend = {};
        backend.header.type = FFX_API_CREATE_CONTEXT_DESC_TYPE_BACKEND_DX12;
        backend.device = s_D3D12->GetDevice();

        ffxCreateContextDescUpscaleVersion version = {};
        version.header.type = FFX_API_CREATE_CONTEXT_DESC_TYPE_UPSCALE_VERSION;
        version.version = FFX_UPSCALER_VERSION;

        ffxCreateContextDescUpscale create = {};
        create.header.type = FFX_API_CREATE_CONTEXT_DESC_TYPE_UPSCALE;
        create.flags = p.createFlags;
        create.maxRenderSize = {p.maxRenderWidth, p.maxRenderHeight};
        create.maxUpscaleSize = {p.displayWidth, p.displayHeight};
        create.fpMessage = OnFfxMessage;

        create.header.pNext = &backend.header;
        backend.header.pNext = &version.header;

        ffxContext context = nullptr;
        ffxReturnCode_t result = s_Ffx.CreateContext(&context, &create.header, nullptr);
        slot.lastResult = static_cast<int32_t>(result);
        if (result != FFX_API_RETURN_OK || context == nullptr)
        {
            Logf(kUnityLogTypeError, "FSR: creating the upscaler failed (%u) for %ux%u -> %ux%u, flags 0x%x.",
                 result, p.maxRenderWidth, p.maxRenderHeight, p.displayWidth, p.displayHeight, p.createFlags);
            return false;
        }

        slot.context = context;
        slot.maxRenderWidth = p.maxRenderWidth;
        slot.maxRenderHeight = p.maxRenderHeight;
        slot.displayWidth = p.displayWidth;
        slot.displayHeight = p.displayHeight;
        slot.createFlags = p.createFlags;
        slot.dispatches = 0;

        ffxQueryGetProviderVersion provider = {};
        provider.header.type = FFX_API_QUERY_DESC_TYPE_GET_PROVIDER_VERSION;
        if (s_Ffx.Query(&slot.context, &provider.header) == FFX_API_RETURN_OK && provider.versionName != nullptr)
        {
            slot.versionId = provider.versionId;
            strncpy_s(slot.versionName, provider.versionName, _TRUNCATE);
        }

        Logf(kUnityLogTypeLog, "FSR: upscaler %s created for %ux%u -> %ux%u, flags 0x%x.",
             slot.versionName[0] ? slot.versionName : "(unknown version)",
             p.maxRenderWidth, p.maxRenderHeight, p.displayWidth, p.displayHeight, p.createFlags);
        return true;
    }

    void Dispatch(const DispatchParams& p)
    {
        if (s_D3D12 == nullptr || s_LoadStatus != StatusReady || p.contextId < 0 || p.contextId >= MaxContexts)
        {
            return;
        }

        std::lock_guard<std::mutex> guard(s_Lock);
        ReleaseRetired(false);

        Slot& slot = s_Slots[p.contextId];
        if (!slot.reserved)
        {
            return;
        }

        // Taken and cleared at once, so a texture from this frame can never be
        // used by a later dispatch whose own events went missing.
        ID3D12Resource* color = slot.textures[TextureColor];
        ID3D12Resource* depth = slot.textures[TextureDepth];
        ID3D12Resource* motion = slot.textures[TextureMotion];
        ID3D12Resource* output = slot.textures[TextureOutput];
        ID3D12Resource* reactive = slot.textures[TextureReactive];
        for (ID3D12Resource*& texture : slot.textures)
        {
            texture = nullptr;
        }

        if (color == nullptr || depth == nullptr || motion == nullptr || output == nullptr)
        {
            slot.lastResult = -1;
            return;
        }

        if (slot.context == nullptr ||
            slot.maxRenderWidth != p.maxRenderWidth || slot.maxRenderHeight != p.maxRenderHeight ||
            slot.displayWidth != p.displayWidth || slot.displayHeight != p.displayHeight ||
            slot.createFlags != p.createFlags)
        {
            Retire(slot);
            if (!Create(slot, p))
            {
                return;
            }
        }

        UnityGraphicsD3D12RecordingState recording = {};
        if (!s_D3D12->CommandRecordingState(&recording) || recording.commandList == nullptr)
        {
            slot.lastResult = -2;
            return;
        }

        const D3D12_RESOURCE_STATES read = D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE;
        const D3D12_RESOURCE_STATES write = D3D12_RESOURCE_STATE_UNORDERED_ACCESS;
        s_D3D12->RequestResourceState(color, read);
        s_D3D12->RequestResourceState(depth, read);
        s_D3D12->RequestResourceState(motion, read);
        s_D3D12->RequestResourceState(output, write);
        if (reactive != nullptr)
        {
            s_D3D12->RequestResourceState(reactive, read);
        }

        ffxDispatchDescUpscale dispatch = {};
        dispatch.header.type = FFX_API_DISPATCH_DESC_TYPE_UPSCALE;
        dispatch.commandList = recording.commandList;
        dispatch.color = ffxApiGetResourceDX12(color, FFX_API_RESOURCE_STATE_COMPUTE_READ);
        dispatch.depth = ffxApiGetResourceDX12(depth, FFX_API_RESOURCE_STATE_COMPUTE_READ);
        dispatch.motionVectors = ffxApiGetResourceDX12(motion, FFX_API_RESOURCE_STATE_COMPUTE_READ);
        dispatch.output = ffxApiGetResourceDX12(output, FFX_API_RESOURCE_STATE_UNORDERED_ACCESS);
        if (reactive != nullptr)
        {
            dispatch.reactive = ffxApiGetResourceDX12(reactive, FFX_API_RESOURCE_STATE_COMPUTE_READ);
        }
        dispatch.jitterOffset = {p.jitterX, p.jitterY};
        dispatch.motionVectorScale = {p.motionScaleX, p.motionScaleY};
        dispatch.renderSize = {p.renderWidth, p.renderHeight};
        dispatch.upscaleSize = {p.displayWidth, p.displayHeight};
        dispatch.enableSharpening = p.sharpen != 0;
        dispatch.sharpness = p.sharpness;
        dispatch.frameTimeDelta = p.frameTimeDeltaMs;
        dispatch.preExposure = p.preExposure;
        dispatch.reset = p.reset != 0;
        dispatch.cameraNear = p.cameraNear;
        dispatch.cameraFar = p.cameraFar;
        dispatch.cameraFovAngleVertical = p.fovVerticalRadians;
        dispatch.viewSpaceToMetersFactor = 1.0f;
        dispatch.flags = p.debugView != 0 ? FFX_UPSCALE_FLAG_DRAW_DEBUG_VIEW : 0;

        ffxReturnCode_t result = s_Ffx.Dispatch(&slot.context, &dispatch.header);
        slot.lastResult = static_cast<int32_t>(result);
        slot.dispatches++;

        // FSR hands every resource back in the state it was given, so these
        // confirm what Unity already asked for. The output was written as a UAV,
        // which is what the last argument tells Unity.
        s_D3D12->NotifyResourceState(color, read, false);
        s_D3D12->NotifyResourceState(depth, read, false);
        s_D3D12->NotifyResourceState(motion, read, false);
        s_D3D12->NotifyResourceState(output, write, true);
        if (reactive != nullptr)
        {
            s_D3D12->NotifyResourceState(reactive, read, false);
        }

        if (result != FFX_API_RETURN_OK && slot.dispatches <= 3)
        {
            Logf(kUnityLogTypeError, "FSR: dispatch failed (%u).", result);
        }
    }

    void Release(int32_t contextId)
    {
        if (contextId < 0 || contextId >= MaxContexts)
        {
            return;
        }

        std::lock_guard<std::mutex> guard(s_Lock);
        Slot& slot = s_Slots[contextId];
        Retire(slot);
        slot = Slot();
        ReleaseRetired(false);
    }

    // A texture event: record which D3D12 resource Unity's texture ID names.
    // Nothing is uploaded; leaving texData null tells Unity so.
    void UNITY_INTERFACE_API OnTextureEvent(int eventId, void* data)
    {
        if (eventId != kUnityRenderingExtEventUpdateTextureBeginV2 || data == nullptr)
        {
            return;
        }

        auto* update = static_cast<UnityRenderingExtTextureUpdateParamsV2*>(data);
        update->texData = nullptr;

        uint32_t contextId = update->userData >> 4;
        uint32_t which = update->userData & 0xF;
        if (s_D3D12 == nullptr || contextId >= MaxContexts || which >= TextureCount)
        {
            return;
        }

        ID3D12Resource* resource = s_D3D12->TextureFromNativeTexture(static_cast<UnityTextureID>(update->textureID));
        std::lock_guard<std::mutex> guard(s_Lock);
        s_Slots[contextId].textures[which] = resource;
    }

    void UNITY_INTERFACE_API OnRenderEvent(int eventId, void* data)
    {
        switch (eventId)
        {
            case EventDispatch:
                if (data != nullptr)
                {
                    Dispatch(*static_cast<const DispatchParams*>(data));
                }
                break;
            case EventRelease:
                Release(static_cast<int32_t>(reinterpret_cast<intptr_t>(data)));
                break;
            default:
                break;
        }
    }

    void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType type)
    {
        if (type == kUnityGfxDeviceEventInitialize)
        {
            if (s_Graphics->GetRenderer() != kUnityGfxRendererD3D12)
            {
                s_D3D12 = nullptr;
                return;
            }

            s_D3D12 = s_Unity->Get<IUnityGraphicsD3D12v8>();
            if (s_D3D12 == nullptr)
            {
                return;
            }

            // Render thread, Unity's own command list, and worker threads
            // synced so everything recorded before the event lands before FSR.
            // FSR binds its own heaps and root signature, so Unity must rebind.
            UnityD3D12PluginEventConfig config = {};
            config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
            config.flags = kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState |
                           kUnityD3D12EventConfigFlag_SyncWorkerThreads;
            config.ensureActiveRenderTextureIsBound = false;
            s_D3D12->ConfigureEvent(EventDispatch, &config);
            s_D3D12->ConfigureEvent(EventRelease, &config);
        }
        else if (type == kUnityGfxDeviceEventShutdown)
        {
            std::lock_guard<std::mutex> guard(s_Lock);
            for (Slot& slot : s_Slots)
            {
                Retire(slot);
                slot = Slot();
            }
            ReleaseRetired(true);
            s_D3D12 = nullptr;
        }
    }
}

extern "C"
{
    void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unity)
    {
        s_Unity = unity;
        s_Graphics = unity->Get<IUnityGraphics>();
        s_Log = unity->Get<IUnityLog>();
        s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);

        // Loaded after the device already exists, so run the start-up by hand.
        OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
    }

    void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
    {
        if (s_Graphics != nullptr)
        {
            s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
        }
    }

    // Main thread. Loads AMD's DLLs and says whether FSR can run at all.
    int32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API ScFfx_Initialize()
    {
        if (s_Unity == nullptr || s_Graphics == nullptr)
        {
            return StatusNoUnityInterface;
        }
        if (s_Graphics->GetRenderer() != kUnityGfxRendererD3D12 || s_D3D12 == nullptr)
        {
            return StatusNotD3D12;
        }
        return LoadFfx();
    }

    UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API ScFfx_GetRenderEventFunc()
    {
        return OnRenderEvent;
    }

    UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API ScFfx_GetTextureEventFunc()
    {
        return OnTextureEvent;
    }

    int32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API ScFfx_EventDispatch() { return EventDispatch; }
    int32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API ScFfx_EventRelease() { return EventRelease; }
    int32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API ScFfx_ParamsSize() { return sizeof(DispatchParams); }

    // Main thread. Reserves a context slot for one camera; -1 when all are taken.
    int32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API ScFfx_ReserveContext()
    {
        std::lock_guard<std::mutex> guard(s_Lock);
        for (int i = 0; i < MaxContexts; i++)
        {
            if (!s_Slots[i].reserved)
            {
                s_Slots[i] = Slot();
                s_Slots[i].reserved = true;
                return i;
            }
        }
        return -1;
    }

    // Main thread. What the context is running, for the F3 overlay and checks.
    // Returns the number of dispatches so far, or -1 for an unknown slot.
    int32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API ScFfx_GetContextInfo(
        int32_t contextId, char* versionName, int32_t versionNameLength, int32_t* lastResult)
    {
        if (contextId < 0 || contextId >= MaxContexts)
        {
            return -1;
        }

        std::lock_guard<std::mutex> guard(s_Lock);
        const Slot& slot = s_Slots[contextId];
        if (versionName != nullptr && versionNameLength > 0)
        {
            strncpy_s(versionName, versionNameLength, slot.versionName, _TRUNCATE);
        }
        if (lastResult != nullptr)
        {
            *lastResult = slot.lastResult;
        }
        return slot.dispatches;
    }
}
