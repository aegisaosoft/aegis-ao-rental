namespace CarRental.Api.Services;

public interface IPythonProcessor
{
    /// <summary>
    /// Обрабатывает изображение через Python скрипт remove_cars_pro.py
    /// </summary>
    /// <param name="inputImagePath">Путь к входному файлу</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Путь к обработанному файлу или null при ошибке</returns>
    Task<string?> ProcessImageAsync(string inputImagePath, CancellationToken ct = default);
}
