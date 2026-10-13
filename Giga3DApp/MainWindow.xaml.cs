using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Giga3DApp.Models;
using Giga3DApp.Services;

namespace Giga3DApp;

public partial class MainWindow : Window
{
    private readonly GigaChatClient _client = new();
    private List<GigaChatModel> _models = new();
    private string _latestModelPath = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        StatusText.Text = "Подключение и загрузка списка моделей...";
        SetupViewport();
    }

    private void SetupViewport()
    {
        var camera = new PerspectiveCamera
        {
            Position = new Point3D(10, 8, 12),
            LookDirection = new Vector3D(-10, -8, -12),
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = 45
        };
        Viewport.Camera = camera;

        var ambient = new AmbientLight(Color.FromRgb(120, 120, 120));
        var directional = new DirectionalLight(Colors.White, new Vector3D(-1, -2, -3));
        var directional2 = new DirectionalLight(Colors.White, new Vector3D(1, 2, 3));

        var sceneGroup = new Model3DGroup();
        sceneGroup.Children.Add(ambient);
        sceneGroup.Children.Add(directional);
        sceneGroup.Children.Add(directional2);

        var floorMesh = new MeshGeometry3D();
        var floorSize = 16;
        var floor = new GeometryModel3D(
            BuildFloorMesh(floorSize),
            new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(236, 240, 245)))
        );
        floor.Transform = new Transform3DGroup();

        var sceneVisual = new ModelVisual3D { Content = sceneGroup };
        var floorVisual = new ModelVisual3D { Content = floor };

        Viewport.Children.Clear();
        Viewport.Children.Add(sceneVisual);
        Viewport.Children.Add(floorVisual);
    }

    private static MeshGeometry3D BuildFloorMesh(double size)
    {
        var mesh = new MeshGeometry3D();
        var half = size / 2;

        double[] pts =
        {
            -half, 0, -half,
             half, 0, -half,
             half, 0,  half,
            -half, 0,  half
        };

        int[] indices = { 0, 1, 2, 0, 2, 3 };

        for (var i = 0; i < 4; i++)
        {
            var p = new Point3D(pts[i * 3], pts[i * 3 + 1], pts[i * 3 + 2]);
            mesh.Positions.Add(p);
        }

        foreach (var idx in indices)
            mesh.TriangleIndices.Add(idx);

        return mesh;
    }

    private async void LoadModelsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadModelsButton.IsEnabled = false;
            StatusText.Text = "Запрашиваю доступный список моделей из GigaChat...";

            _models = await _client.GetModelsAsync();
            ModelComboBox.ItemsSource = _models.Select(m => m.Id).ToList();
            ModelComboBox.IsEnabled = _models.Count > 0;
            GenerateButton.IsEnabled = _models.Count > 0;
            OpenModelButton.IsEnabled = false;

            if (_models.Count == 0)
            {
                StatusText.Text = "Список моделей пуст. Проверьте токен доступа и доступность API.";
                return;
            }

            ModelComboBox.SelectedIndex = 0;
            StatusText.Text = $"Загружено {_models.Count} моделей. Можно генерировать 3D-ассет.";
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (ex.InnerException is not null)
            {
                message += $" | Внутренняя ошибка: {ex.InnerException.Message}";
            }

            StatusText.Text = $"Ошибка при загрузке моделей: {message}";
        }
        finally
        {
            LoadModelsButton.IsEnabled = true;
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModelComboBox.SelectedItem is not string selectedModelId)
        {
            StatusText.Text = "Сначала выберите модель.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
        {
            StatusText.Text = "Введите текстовое описание для генерации 3D-модели.";
            return;
        }

        try
        {
            GenerateButton.IsEnabled = false;
            OpenModelButton.IsEnabled = false;
            StatusText.Text = "Отправляю запрос в GigaChat и строю 3D-модель...";

            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GigaChat3DAssets");
            Directory.CreateDirectory(outputFolder);
            StatusText.Text = "GigaChat создаёт 3D-модель. Это может занять некоторое время...";
            var generatedModel = await _client.Generate3DModelAsync(selectedModelId, PromptTextBox.Text, outputFolder);
            _latestModelPath = generatedModel.FilePath;

            OutputPathText.Text = $"Файл: {_latestModelPath}";
            StatusText.Text = $"GigaChat создал 3D-модель ({generatedModel.MediaType}). Файл сохранён: {_latestModelPath}";
            PreviewHint.Text = "Модель создана самим GigaChat и сохранена в исходном формате. Нажмите «Открыть 3D-файл», чтобы просмотреть её.";
            OpenModelButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (ex.InnerException is not null)
            {
                message += $" | Внутренняя ошибка: {ex.InnerException.Message}";
            }

            StatusText.Text = $"Ошибка генерации: {message}";
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private void OpenModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestModelPath) || !File.Exists(_latestModelPath))
        {
            StatusText.Text = "Файл модели не найден. Сначала сгенерируйте 3D-модель ещё раз.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_latestModelPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Не удалось открыть модель: {ex.Message}. Файл сохранён по адресу: {_latestModelPath}";
        }
    }
}
