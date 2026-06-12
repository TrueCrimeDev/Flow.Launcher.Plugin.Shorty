using CommunityToolkit.Mvvm.ComponentModel;

namespace Shorty.Models;

public sealed class Preset : ObservableObject
{
    private string _name = "";
    private string _system = "";
    private string _model = "";
    private double _temperature = 0.2;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string System
    {
        get => _system;
        set
        {
            if (SetProperty(ref _system, value))
            {
                OnPropertyChanged(nameof(SystemPreview));
            }
        }
    }

    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public double Temperature
    {
        get => _temperature;
        set => SetProperty(ref _temperature, Math.Clamp(value, 0, 2));
    }

    public string SystemPreview
    {
        get
        {
            var value = (System ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= 140 ? value : value[..137] + "...";
        }
    }

    public Preset Clone()
    {
        return new Preset
        {
            Name = Name,
            System = System,
            Model = Model,
            Temperature = Temperature
        };
    }
}
