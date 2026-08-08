using Microsoft.AspNetCore.Components;
using Paages.Domain.Interfaces;
using Paages.Infrastructure.Services;
using Paages.Web.Interfaces;

class NavigationController(NoteService NoteService, NavigationManager Navigation, IUIController UIController): INavigationController
{
    public async Task OpenNote(Guid noteId)
    {
        Navigation.NavigateTo($"/notes/{noteId}");

        if (await NoteService.HasFolder(noteId))
            UIController.InvokeFolderExpansion(await NoteService.GetAllDescendantFolderIdsAsync(noteId));
    }
}