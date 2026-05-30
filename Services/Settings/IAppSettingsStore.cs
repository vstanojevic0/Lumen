namespace Lumen.Services.Settings;

public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
