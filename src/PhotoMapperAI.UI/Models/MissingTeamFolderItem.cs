using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoMapperAI.UI.Models;

/// <summary>
/// Represents a team missing a photo folder in the input directory.
/// </summary>
public partial class MissingTeamFolderItem : ObservableObject
{
    [ObservableProperty]
    private int _teamId;

    [ObservableProperty]
    private string _teamName = string.Empty;
}
