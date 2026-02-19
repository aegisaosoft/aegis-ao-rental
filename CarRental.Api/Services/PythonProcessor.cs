using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CarRental.Api.Services;

/// <summary>
/// Вызывает Python скрипт remove_cars_pro.py для обработки изображений
/// Поддерживает Windows (локальная разработка) и Linux (Azure App Service)
/// </summary>
public class PythonProcessor : IPythonProcessor
{
    private readonly ILogger<PythonProcessor> _logger;
    private const int TimeoutSeconds = 120;

    // Windows: локальный путь для разработки
    private const string WindowsScriptPath = @"C:\aegis-ao\cars\remove_cars_pro.py";
    private const string WindowsPythonExe = "python";

    // Linux/Azure: скрипт рядом с приложением, Python в venv
    private const string LinuxScriptPath = "/home/site/wwwroot/python/remove_cars_pro.py";
    private const string LinuxPythonExe = "/home/python_venv/bin/python3";
    // Fallback если venv не установлен
    private const string LinuxPythonFallback = "python3";

    public PythonProcessor(ILogger<PythonProcessor> logger)
    {
        _logger = logger;
    }

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private string GetScriptPath() => IsWindows ? WindowsScriptPath : LinuxScriptPath;

    private string GetPythonExe()
    {
        if (IsWindows) return WindowsPythonExe;

        // На Linux проверяем наличие venv
        if (File.Exists(LinuxPythonExe))
            return LinuxPythonExe;

        return LinuxPythonFallback;
    }

    public async Task<string?> ProcessImageAsync(string inputImagePath, CancellationToken ct = default)
    {
        var scriptPath = GetScriptPath();
        var scriptDir = Path.GetDirectoryName(scriptPath)!;
        var inputDir = Path.Combine(scriptDir, "cars_input");
        var outputDir = Path.Combine(scriptDir, "cars_output");

        try
        {
            // Проверяем наличие скрипта
            if (!File.Exists(scriptPath))
            {
                _logger.LogError("PythonProcessor: Script not found at {ScriptPath}", scriptPath);
                return null;
            }

            // Очищаем input директорию
            if (Directory.Exists(inputDir))
            {
                foreach (var f in Directory.GetFiles(inputDir))
                    File.Delete(f);
            }
            else
            {
                Directory.CreateDirectory(inputDir);
            }

            // Создаём output директорию
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            // Копируем файл в cars_input/
            var inputFileName = Path.GetFileName(inputImagePath);
            var destPath = Path.Combine(inputDir, inputFileName);
            File.Copy(inputImagePath, destPath, overwrite: true);

            var pythonExe = GetPythonExe();
            _logger.LogInformation("PythonProcessor: Processing {FileName} with {Python} on {OS}",
                inputFileName, pythonExe, IsWindows ? "Windows" : "Linux");

            // Запускаем Python скрипт
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = scriptDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Устанавливаем UTF-8 для Python stdout/stderr
            psi.Environment["PYTHONIOENCODING"] = "utf-8";

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds), ct);

            var stdoutText = await stdout;
            var stderrText = await stderr;

            if (process.ExitCode != 0)
            {
                _logger.LogError("PythonProcessor: ExitCode={ExitCode}, stderr={Stderr}",
                    process.ExitCode, stderrText);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(stdoutText))
                _logger.LogInformation("PythonProcessor stdout: {Output}", stdoutText.Trim());

            // Ищем обработанный файл в cars_output/
            // Скрипт сохраняет с тем же именем но .png расширением
            var outputFileName = Path.GetFileNameWithoutExtension(inputFileName) + ".png";
            var outputPath = Path.Combine(outputDir, outputFileName);

            if (File.Exists(outputPath))
            {
                _logger.LogInformation("PythonProcessor: Successfully processed → {OutputPath}", outputPath);
                return outputPath;
            }

            // Попробуем найти любой новый файл
            var outputFiles = Directory.GetFiles(outputDir, "*.png");
            if (outputFiles.Length > 0)
            {
                var latest = outputFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
                _logger.LogInformation("PythonProcessor: Found output file {File}", latest);
                return latest;
            }

            _logger.LogWarning("PythonProcessor: No output file found after processing");
            return null;
        }
        catch (TimeoutException)
        {
            _logger.LogError("PythonProcessor: Timeout after {Timeout}s", TimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PythonProcessor: Error processing {InputPath}", inputImagePath);
            return null;
        }
    }
}
