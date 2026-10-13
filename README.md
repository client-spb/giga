# GigaChat 3D Asset Generator

Пример Windows WPF-приложения, которое:

- запрашивает список моделей из GigaChat API;
- позволяет выбрать модель;
- отправляет текстовый запрос в выбранную модель;
- генерирует простой 3D-ассет и текстуру на локальном компьютере.

Важно: GigaChat API сам по себе не умеет напрямую конвертировать текст в полноценный 3D-модель в формате .glb/.gltf с готовыми текстурами. В этом примере модель GigaChat используется для создания JSON-спецификации объекта, а затем локальный код процедурно строит низкополигональную 3D-модель и PNG-текстуру.

## Сборка

1. Откройте терминал в папке проекта:
   
   cd /workspaces/giga/Giga3DApp

2. Сборка:

   dotnet build -p:EnableWindowsTargeting=true -p:UseSharedCompilation=false

3. Артефакт будет в:

   Giga3DApp/bin/Debug/net8.0-windows/

## Запуск

На Windows:

1. Откройте собранный .dll или запустите приложение через Visual Studio / dotnet.
2. Нажмите "Загрузить модели".
3. Выберите модель.
4. Введите текстовое описание объекта.
5. Нажмите "Сгенерировать 3D".

Результат сохраняется в папке:

C:\Users\<Ваш_Пользователь>\Documents\GigaChat3DAssets

## Основные файлы

- Giga3DApp/MainWindow.xaml — интерфейс приложения
- Giga3DApp/MainWindow.xaml.cs — логика UI
- Giga3DApp/Services/GigaChatClient.cs — OAuth и запросы к GigaChat
- Giga3DApp/Services/ThreeDAssetBuilder.cs — генерация OBJ/MTL и PNG-текстуры
- Giga3DApp/Models/GigaChatModelsResponse.cs — модели данных

## Ключ API

В коде уже зашит ваш ключ как:

GigaChatApi.AUTH_KEY

Это пример для личного использования, как вы просили.
