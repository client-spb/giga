# GigaChat 3D Model Generator

Windows WPF-приложение, которое:

- запрашивает список моделей из GigaChat API;
- позволяет выбрать модель;
- вызывает встроенный инструмент GigaChat `model_3d_generate` по текстовому описанию;
- скачивает созданный API файл модели и сохраняет его без подмены процедурной фигурой.

Сохранённый файл находится в `Документы/GigaChat3DAssets`. Приложение открывает его программой Windows, назначенной для этого формата. Встроенный просмотрщик GLB/OBJ пока не подключён.

## Сборка

1. Откройте терминал в папке проекта:
   
   cd /workspaces/giga/Giga3DApp

2. Сборка:

   dotnet build -p:EnableWindowsTargeting=true -p:UseSharedCompilation=false

3. Артефакт будет в:

   Giga3DApp/bin/Debug/net8.0-windows/

## Запуск

На Windows:

1. Запустите собранный exe или приложение через Visual Studio.
2. Нажмите "Загрузить модели".
3. Выберите модель.
4. Введите текстовое описание объекта.
5. Нажмите "Сгенерировать 3D".
6. Нажмите "Открыть 3D-файл", чтобы открыть скачанную модель в назначенной Windows программе.

Результат сохраняется в папке:

C:\Users\<Ваш_Пользователь>\Documents\GigaChat3DAssets

## Основные файлы

- Giga3DApp/MainWindow.xaml — интерфейс приложения
- Giga3DApp/MainWindow.xaml.cs — логика UI
- Giga3DApp/Services/GigaChatClient.cs — OAuth и запросы к GigaChat
- Giga3DApp/Services/ThreeDAssetBuilder.cs — прежняя локальная генерация примитивов; в актуальном сценарии API не используется
- Giga3DApp/Models/GigaChatModelsResponse.cs — модели данных

## Ключ API

В коде уже зашит ваш ключ как:

GigaChatApi.AUTH_KEY

Это пример для личного использования, как вы просили.
