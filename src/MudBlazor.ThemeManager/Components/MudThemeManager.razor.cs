using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor.State;
using MudBlazor.ThemeManager.Extensions;
using System.Collections;
using MudBlazor.Utilities;


namespace MudBlazor.ThemeManager;

public partial class MudThemeManager : ComponentBaseWithState
{
    [Inject]
    private IDialogService DialogService { get; set; }
    
    private static readonly PaletteLight DefaultPaletteLight = new();
    private static readonly PaletteDark DefaultPaletteDark = new();
    private readonly ParameterState<bool> _openState;
    private readonly ParameterState<bool> _isDarkModeState;

    private PaletteLight? _currentPaletteLight;
    private PaletteDark? _currentPaletteDark;
    private Palette _currentPalette;
    private MudTheme? _customTheme;

    public MudThemeManager()
    {
        using var registerScope = CreateRegisterScope();
        _openState = registerScope.RegisterParameter<bool>(nameof(Open))
            .WithParameter(() => Open)
            .WithEventCallback(() => OpenChanged);
        _isDarkModeState = registerScope.RegisterParameter<bool>(nameof(IsDarkMode))
            .WithParameter(() => IsDarkMode)
            .WithChangeHandler(OnIsDarkModeChanged);
        _currentPalette = GetPalette();
    }

    public ThemePreset? SelectedThemePreset { get; set; } = null;

    [Parameter] public bool Open { get; set; } = true;

    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    [Parameter]
    public MudTheme? Theme { get; set; }

    [Parameter]
    public bool IsDarkMode { get; set; }

    [Parameter]
    public ColorPickerView ColorPickerView { get; set; } = ColorPickerView.Spectrum;

    [Parameter]
    public EventCallback<MudTheme> ThemeChanged { get; set; }
    
    protected override void OnInitialized()
    {
        base.OnInitialized();

        _currentPalette = GetPalette();

        if (Theme is null)
        {
            return;
        }

        _customTheme = Theme.DeepClone();
        _currentPaletteLight = Theme.PaletteLight.DeepClone();
        _currentPaletteDark = Theme.PaletteDark.DeepClone();
    }
    
    private bool _exportVisible;
    private readonly DialogOptions _exportDialogOptions = new() { FullWidth = true };

    private void ToggleExportDialog() => _exportVisible = !_exportVisible;

private string _jsonExport = string.Empty;
private string _cSharpExport = string.Empty;

private async Task ApplyThemePreset()
{
    if (SelectedThemePreset != null)
    {
        bool? result = await DialogService.ShowMessageBox("Notice", $"Are you sure you want to override the current theme with the {SelectedThemePreset.PresetName} theme?", yesText: "Apply Theme", cancelText: "Cancel");
        if (result.HasValue && result.Value)
        {
            Theme = SelectedThemePreset.Theme;
            await ThemeChanged.InvokeAsync(Theme);
            SelectedThemePreset = null;
            StateHasChanged();
        }
    }
}

    private void ExportTheme()
    {
        ToggleExportDialog();
        _jsonExport = System.Text.Json.JsonSerializer.Serialize(_customTheme);
    }
    
    private async Task RevertTheme()
    {
        bool? result = await DialogService.ShowMessageBox("Notice", "Are you sure you want to revert this theme to the default?", yesText: "Revert Changes", cancelText: "Cancel");
        if (result.HasValue && result.Value)
        {
            _customTheme = Theme.DeepClone();
            _currentPaletteLight = DefaultPaletteLight.DeepClone();
            _currentPaletteDark = DefaultPaletteDark.DeepClone();

            GetPalette();
            StateHasChanged();
        }
    }
    
    private async Task ImportTheme(IBrowserFile file)
    {
        try
        {
            if (file != null)
            {
                bool? result = await DialogService.ShowMessageBox("Notice", "Are you sure you want to override the current theme with the imported one?", yesText: "Import Theme", cancelText: "Cancel");
                if (result.HasValue && result.Value)
                {
                    const long maxFileSize = 1024 * 1024 * 10; // 10 MB limit
                    using var stream = file.OpenReadStream(maxFileSize);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string fileContents = await reader.ReadToEndAsync();
                
                    _customTheme = JsonSerializer.Deserialize<MudTheme>(fileContents);
                    
                    UpdateCustomTheme();
                    
                    if (_isDarkModeState.Value)
                    {
                        _currentPaletteDark = _customTheme.PaletteDark;
                        Theme.PaletteDark = _customTheme.PaletteDark;
                    }
                    else
                    {
                        _currentPaletteLight = _customTheme.PaletteLight;
                        Theme.PaletteLight = _customTheme.PaletteLight;
                    }
                    
                    await UpdateThemeChangedAsync();
                }
            }
        } catch (Exception ex)
        {
        }
    }
    
    public string FontFamily { get; set; } = "Roboto";
    private int DrawerWidthRight = 240;
    private int DrawerWidthLeft = 240;
    private int DrawerMiniWidthRight = 56;
    private int DrawerMiniWidthLeft = 56;
    private int AppbarHeight = 64;
    private int ExtraSmall = 0;
    private int Small = 600;
    private int Medium = 960;
    private int Large = 1280;
    private int ExtraLarge = 1920;
    private int ExtraExtraLarge = 2560;
    
    private bool _colorPickerOpen = false;
    
    private MudColor? ThemeColor { get; set; }
    private ThemePaletteColor ColorType { get; set; }
    private MudColor? _setupPalleteColor { get; set; }
    private void SetupColorPicker(MudColor colorValue, ThemePaletteColor colorType)
    {
        _setupPalleteColor = colorValue;
        ColorType = colorType;
        _colorPickerOpen = true;
    }
    
    public Task UpdateColor(MudColor value)
    {
        _setupPalleteColor = value;
        ThemeColor = value;
        var newPaletteColor = new ThemeUpdatedValue
        {
            ColorStringValue = value.ToString(),
            ThemePaletteColor = ColorType
        };

        return UpdatePalette(newPaletteColor);
    }
    

    private ZIndex _customZIndex = new();
    
    public Task UpdatePalette(ThemeUpdatedValue value)
    {
        UpdateCustomTheme();

        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }

        Palette newPalette = _isDarkModeState.Value ? _customTheme.PaletteDark : _customTheme.PaletteLight;
        
        
        switch (value.ThemePaletteColor)
        {
            case ThemePaletteColor.Primary:
                newPalette.Primary = value.ColorStringValue;
                break;
            case ThemePaletteColor.PrimaryLighten:
                newPalette.PrimaryLighten = value.ColorStringValue;
                break;
            case ThemePaletteColor.PrimaryDarken:
                newPalette.PrimaryDarken = value.ColorStringValue;
                break;
            case ThemePaletteColor.Secondary:
                newPalette.Secondary = value.ColorStringValue;
                break;
            case ThemePaletteColor.Tertiary:
                newPalette.Tertiary = value.ColorStringValue;
                break;
            case ThemePaletteColor.Info:
                newPalette.Info = value.ColorStringValue;
                break;
            case ThemePaletteColor.Success:
                newPalette.Success = value.ColorStringValue;
                break;
            case ThemePaletteColor.Warning:
                newPalette.Warning = value.ColorStringValue;
                break;
            case ThemePaletteColor.Error:
                newPalette.Error = value.ColorStringValue;
                break;
            case ThemePaletteColor.Dark:
                newPalette.Dark = value.ColorStringValue;
                break;
            case ThemePaletteColor.Surface:
                newPalette.Surface = value.ColorStringValue;
                break;
            case ThemePaletteColor.Background:
                newPalette.Background = value.ColorStringValue;
                break;
            case ThemePaletteColor.BackgroundGray:
                newPalette.BackgroundGray = value.ColorStringValue;
                break;
            case ThemePaletteColor.DrawerText:
                newPalette.DrawerText = value.ColorStringValue;
                break;
            case ThemePaletteColor.DrawerIcon:
                newPalette.DrawerIcon = value.ColorStringValue;
                break;
            case ThemePaletteColor.DrawerBackground:
                newPalette.DrawerBackground = value.ColorStringValue;
                break;
            case ThemePaletteColor.AppbarText:
                newPalette.AppbarText = value.ColorStringValue;
                break;
            case ThemePaletteColor.AppbarBackground:
                newPalette.AppbarBackground = value.ColorStringValue;
                break;
            case ThemePaletteColor.LinesDefault:
                newPalette.LinesDefault = value.ColorStringValue;
                break;
            case ThemePaletteColor.LinesInputs:
                newPalette.LinesInputs = value.ColorStringValue;
                break;
            case ThemePaletteColor.Divider:
                newPalette.Divider = value.ColorStringValue;
                break;
            case ThemePaletteColor.DividerLight:
                newPalette.DividerLight = value.ColorStringValue;
                break;
            case ThemePaletteColor.TextPrimary:
                newPalette.TextPrimary = value.ColorStringValue;
                break;
            case ThemePaletteColor.TextSecondary:
                newPalette.TextSecondary = value.ColorStringValue;
                break;
            case ThemePaletteColor.TextDisabled:
                newPalette.TextDisabled = value.ColorStringValue;
                break;
            case ThemePaletteColor.ActionDefault:
                newPalette.ActionDefault = value.ColorStringValue;
                break;
            case ThemePaletteColor.ActionDisabled:
                newPalette.ActionDisabled = value.ColorStringValue;
                break;
            case ThemePaletteColor.ActionDisabledBackground:
                newPalette.ActionDisabledBackground = value.ColorStringValue;
                break;
            case ThemePaletteColor.Skeleton:
                newPalette.Skeleton = value.ColorStringValue;
                break;
        }

        if (_isDarkModeState.Value)
        {
            _customTheme.PaletteDark = (PaletteDark)newPalette;
        }
        else
        {
            _customTheme.PaletteLight = (PaletteLight)newPalette;
        }
        if (_isDarkModeState.Value)
        {
            _currentPaletteDark = _customTheme.PaletteDark;
            Theme.PaletteDark = _customTheme.PaletteDark;
        }
        else
        {
            _currentPaletteLight = _customTheme.PaletteLight;
            Theme.PaletteLight = _customTheme.PaletteLight;
        }

        return UpdateThemeChangedAsync();
    }

    private Task UpdateOpenValueAsync() => _openState.SetValueAsync(false);
    
        public static string GenerateCSharpCode(object obj, string varName = "obj", int indentLevel = 0)
        {
            if (obj == null) return "null";

            Type type = obj.GetType();
            var indent = new string(' ', indentLevel * 4);
            var sb = new StringBuilder();

            sb.AppendLine($"{indent}var {varName} = new {type.Name}");
            sb.AppendLine($"{indent}{{");

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = prop.GetValue(obj);
                string valueStr = FormatValue(value, prop.PropertyType, indentLevel + 1);
                sb.AppendLine($"{indent}    {prop.Name} = {valueStr},");
            }

            sb.AppendLine($"{indent}}};");
            return sb.ToString();
        }

        private static string FormatValue(object value, Type type, int indentLevel)
        {
            if (value == null) return "null";

            if (type == typeof(string)) return $"\"{value}\"";
            if (type == typeof(char)) return $"'{value}'";
            if (type == typeof(bool)) return value.ToString().ToLower();
            if (type.IsPrimitive || type.IsEnum) return value.ToString();

            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                var sb = new StringBuilder();
                sb.AppendLine("new []");
                sb.AppendLine(new string(' ', indentLevel * 4) + "{");

                foreach (var item in (IEnumerable)value)
                {
                    sb.AppendLine(new string(' ', (indentLevel + 1) * 4) + FormatValue(item, item.GetType(), indentLevel + 1) + ",");
                }

                sb.Append(new string(' ', indentLevel * 4) + "}");
                return sb.ToString();
            }

            // Nested object
            return GenerateCSharpCode(value, "", indentLevel).TrimEnd(';');
        }

    
    private Task OnDrawerZIndexAsync(int value)
    {
        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }
        _customZIndex.Drawer = value;
        Theme.ZIndex = _customZIndex;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnPopoverZIndexAsync(int value)
    {
        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }
        
        _customZIndex.Popover = value;
        Theme.ZIndex = _customZIndex;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnAppBarZIndexAsync(int value)
    {
        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }
        
        _customZIndex.AppBar = value;
        Theme.ZIndex = _customZIndex;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnDialogZIndexAsync(int value)
    {
        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }
        
        _customZIndex.Dialog = value;
        Theme.ZIndex = _customZIndex;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnSnackbarZIndexAsync(int value)
    {
        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }
        
        _customZIndex.Snackbar = value;
        Theme.ZIndex = _customZIndex;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnTooltipZIndexAsync(int value)
    {
        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }
        
        _customZIndex.Tooltip = value;
        Theme.ZIndex = _customZIndex;

        return UpdateThemeChangedAsync();
    }

    private async Task UpdateThemeChangedAsync()
    {
        await ThemeChanged.InvokeAsync(Theme);
        StateHasChanged();
    }

    private void OnIsDarkModeChanged(ParameterChangedEventArgs<bool> arg)
    {
        if (_customTheme is not null)
        {
            UpdateCustomTheme();
        }
    }

    // private Task OnDrawerClipModeAsync(DrawerClipMode value)
    // {
    //     if (Theme is null)
    //     {
    //         return Task.CompletedTask;
    //     }
    //
    //     Theme. = value;
    //
    //     return UpdateThemeChangedAsync();
    // }

    private Task OnDefaultBorderRadiusAsync(int value)
    {
        if (Theme is null)
        {
            return Task.CompletedTask;
        }

        if (_customTheme is null)
        {
            return Task.CompletedTask;
        }

        //Theme.LayoutProperties.DefaultBorderRadius = $"{value}px";
        var newBorderRadius = _customTheme.LayoutProperties;

        newBorderRadius.DefaultBorderRadius = $"{value}px";

        _customTheme.LayoutProperties = newBorderRadius;
        Theme = _customTheme;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnDrawerMiniWidthLeftAsync(int value)
    {
        if (Theme is null)
        {
            return Task.CompletedTask;
        }

        if (_customTheme is null)
        {
            return Task.CompletedTask;
        }
        
        DrawerMiniWidthLeft = value;

        var newDrawerMiniWidthLeft = _customTheme.LayoutProperties;

        newDrawerMiniWidthLeft.DrawerMiniWidthLeft = $"{value}px";

        _customTheme.LayoutProperties = newDrawerMiniWidthLeft;
        Theme = _customTheme;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnDrawerMiniWidthRightAsync(int value)
    {
        if (Theme is null)
        {
            return Task.CompletedTask;
        }

        if (_customTheme is null)
        {
            return Task.CompletedTask;
        }
        DrawerMiniWidthRight = value;

        var newDrawerMiniWidthRight = _customTheme.LayoutProperties;

        newDrawerMiniWidthRight.DrawerMiniWidthRight = $"{value}px";

        _customTheme.LayoutProperties = newDrawerMiniWidthRight;
        Theme = _customTheme;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnDrawerWidthLeftAsync(int value)
    {
        if (Theme is null)
        {
            return Task.CompletedTask;
        }

        if (_customTheme is null)
        {
            return Task.CompletedTask;
        }

        DrawerWidthLeft = value;

        var newDrawerWidthLeft = _customTheme.LayoutProperties;

        newDrawerWidthLeft.DrawerWidthLeft = $"{value}px";

        _customTheme.LayoutProperties = newDrawerWidthLeft;
        Theme = _customTheme;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnDrawerWidthRightAsync(int value)
    {
        if (Theme is null)
        {
            return Task.CompletedTask;
        }

        if (_customTheme is null)
        {
            return Task.CompletedTask;
        }

        DrawerWidthRight = value;

        var newDrawerWidthRight = _customTheme.LayoutProperties;

        newDrawerWidthRight.DrawerMiniWidthRight = $"{value}px";

        _customTheme.LayoutProperties = newDrawerWidthRight;
        Theme = _customTheme;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnAppbarHeightAsync(int value)
    {
        if (Theme is null)
        {
            return Task.CompletedTask;
        }

        if (_customTheme is null)
        {
            return Task.CompletedTask;
        }
        
        AppbarHeight = value;

        var newAppbarHeight = _customTheme.LayoutProperties;

        newAppbarHeight.AppbarHeight = $"{value}px";

        _customTheme.LayoutProperties = newAppbarHeight;
        Theme = _customTheme;

        return UpdateThemeChangedAsync();
    }

    private Task OnElevationAsync(int index, string value)
    {
        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }

        _customTheme.Shadows.Elevation[index] = value;
        Theme = _customTheme;

        return UpdateThemeChangedAsync();
    }
    
    private Task OnExtraSmallAsync(int value)
    {
        // if (Theme is null || _customTheme is null)
        // { 
        //     return Task.CompletedTask;
        // }
        //
        // ExtraSmall = value;
        // _customTheme.
        // Theme = _customTheme;
        //
        // return UpdateThemeChangedAsync();
        return null;
    }

    // private Task OnAppBarElevationAsync(int value)
    // {
    //     if (Theme is null)
    //     {
    //         return Task.CompletedTask;
    //     }
    //
    //     Theme.AppBarElevation = value;
    //
    //     return UpdateThemeChangedAsync();
    // }

    // private Task OnDrawerElevationAsync(int value)
    // {
    //     if (Theme is null)
    //     {
    //         return Task.CompletedTask;
    //     }
    //
    //     Theme.DrawerElevation = value;
    //
    //     return UpdateThemeChangedAsync();
    // }

    private Task OnFontFamilyAsync(string value)
    {
        if (Theme is null || _customTheme is null)
        {
            return Task.CompletedTask;
        }

        //Theme.FontFamily = value;
        var newTypography = _customTheme.Typography;

        newTypography.Body1.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.Body2.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.Button.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.Caption.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.Default.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.H1.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.H2.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.H3.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.H4.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.H5.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.H6.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.Overline.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.Subtitle1.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };
        newTypography.Subtitle2.FontFamily = new[] { value, "Helvetica", "Arial", "sans-serif" };

        _customTheme.Typography = newTypography;
        Theme = _customTheme;

        return UpdateThemeChangedAsync();
    }

    private void UpdateCustomTheme()
    {
        if (_customTheme is null)
        {
            return;
        }

        if (_currentPaletteLight is not null)
        {
            _customTheme.PaletteLight = _currentPaletteLight;
        }

        if (_currentPaletteDark is not null)
        {
            _customTheme.PaletteDark = _currentPaletteDark;
        }

        _currentPalette = GetPalette();
    }

    private Palette GetPalette() => _isDarkModeState.Value
        ? _currentPaletteDark ?? DefaultPaletteDark
        : _currentPaletteLight ?? DefaultPaletteLight;
}