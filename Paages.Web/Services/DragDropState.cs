using Microsoft.JSInterop;

namespace Paages.Web.Services;

public class DragDropState(IJSRuntime js)
{
    public Guid? DraggedNodeId { get; private set; }
    public bool DraggedIsFolder { get; private set; }
    private IJSObjectReference? _module;

    public async Task<double> GetRelativeYAsync(string elementId, double clientY)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/dragDrop.js");
        return await _module.InvokeAsync<double>("getRelativeY", elementId, clientY);
    }

    public event Action? Changed;

    public void StartDrag(Guid nodeId, bool isFolder)
    {
        DraggedNodeId = nodeId;
        DraggedIsFolder = isFolder;
        Changed?.Invoke();
    }
    public async Task ClearIndicatorAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/dragDrop.js");
        await _module.InvokeVoidAsync("clearDragIndicator");
    }
    public void EndDrag()
    {
        DraggedNodeId = null;
        Changed?.Invoke();
    }
}