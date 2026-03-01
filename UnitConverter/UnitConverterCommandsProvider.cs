using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UnitConverter.Pages;

namespace UnitConverter;

[System.Runtime.InteropServices.ComVisible(true)]
public sealed partial class UnitConverterCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public UnitConverterCommandsProvider()
    {
        _commands = [
            new CommandItem(new UnitConverterPage())
            {
                Title = "Convert inches ↔ mm",
                Subtitle = "Unit Converter",
                Icon = new IconInfo("\uE8EF")
            }
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}