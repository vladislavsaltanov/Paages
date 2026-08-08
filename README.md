# Paages

Blazor Server-приложение для заметок: иерархия папок вместо тегов, автоформатирование markdown в реальном времени, редактор на Quill.

## Возможности

- Дерево папок произвольной вложенности, drag-and-drop (перенос между родителями, reorder)
- Контекстное меню: переименование, дублирование, закрепление, удаление с подтверждением
- Вкладки, breadcrumbs, автораскрытие активного пути
- Автоформатирование markdown при вводе и вставке (заголовки, списки, цитаты, inline code, code block, bold)
- Reveal сырой разметки при выделении текста
- Автосохранение (debounce 1.5с), Ctrl+S
- JWT-аутентификация в `Paages.Api`: access/refresh, ротация с reuse-detection, абсолютный лимит цепочки 60 дней, rate limiting по IP
- Cookie-аутентификация в `Paages.Web`, изоляция данных по пользователю
- 53 unit-теста (`Paages.Tests`) на SQLite in-memory

## Стек

- .NET 10, C# 12
- Blazor Server (SignalR circuits)
- EF Core + SQLite (WAL)
- Quill 2.0.2 + собственный JS-слой автоформатирования

## Архитектура

```
Paages.Domain           сущности, интерфейсы, доменные исключения
Paages.Infrastructure   EF Core, DbContext, миграции, AuthService, NoteService
Paages.Web              Blazor Server UI, cookie-auth
Paages.Api              JWT auth API (register / login / refresh / logout)
Paages.Tests            xUnit, SQLite in-memory
```

`Paages.Web` и `Paages.Api` работают как отдельные процессы над общей базой SQLite; WAL и `busy_timeout` настроены через общий connection interceptor.

## Быстрый старт

Требования: .NET 10 SDK, `dotnet-ef` (`dotnet tool install --global dotnet-ef`).

```bash
git clone https://github.com/vladislavsaltanov/Paages.git
cd Paages
dotnet ef database update --project Paages.Infrastructure --startup-project Paages.Web
dotnet run --project Paages.Web
```

БД создаётся в `%LocalAppData%\Paages\paages.db`, путь переопределяется ключом `Database:Path`.

Для `Paages.Api` требуется секрет подписи JWT:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<random-string>" --project Paages.Api
dotnet run --project Paages.Api
```

## Тесты

```bash
dotnet test
```

## Roadmap

- CI pipeline
- Google OAuth
- CRUD заметок через `Paages.Api`
- Публикация заметки/папки по отдельной ссылке
- MCP-сервер
- Экспорт в markdown/PDF

## Лицензия

AGPL-3.0. См. `LICENSE.txt`.
