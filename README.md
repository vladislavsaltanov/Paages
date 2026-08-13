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
- Google OAuth (server-side), автолинк к существующему аккаунту по email
- Подтверждение почты (блокирует вход до подтверждения), сброс пароля, смена почты
- Personal Access Token (PAT) для сервисных клиентов - хешируется, отзывается, именуется
- 54 unit-теста (`Paages.Tests`) на SQLite in-memory, CI: build → migrations check → test
- Публикация заметок с последующим получением ссылки на ее просмотр без авторизации. Возможность обновления опубликованной заметки без смены ссылки.

## Стек

- .NET 10, C# 12
- Blazor Server (SignalR circuits)
- EF Core + SQLite (WAL)
- Quill 2.0.2 + собственный JS-слой автоформатирования
- MailKit (SMTP) для писем подтверждения/сброса

## Архитектура

```
Paages.Domain           сущности, интерфейсы, доменные исключения
Paages.Infrastructure   EF Core, DbContext, миграции, AuthService, UserAccountService,
                        AccountTokenService, ApiTokenService, NoteService
Paages.Web              Blazor Server UI, cookie-auth, Google OAuth
Paages.Api              JWT + PAT auth API (register / login / refresh / logout)
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

Для Google OAuth и почты (подтверждение/сброс пароля) требуются дополнительные секреты в `Paages.Web`:

```bash
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>" --project Paages.Web
dotnet user-secrets set "Smtp:Password" "<app-password>" --project Paages.Web
```

`Authentication:Google:ClientId` и SMTP-хост (`smtp.gmail.com:465`, SSL) - не секреты, лежат в `appsettings.json`.

## Тесты

```bash
dotnet test
```

## Roadmap

- `/settings`: UI для никнейма, смены почты, управления PAT
- HTTP-level тесты `Paages.Api` (`WebApplicationFactory`)
- Тесты Stage 3 (`AccountTokenService`, `ApiTokenService`, Google-автолинк)
- CRUD заметок через `Paages.Api`
- Публикация заметки/папки по отдельной ссылке
- MCP-сервер
- Экспорт в markdown/PDF

## Лицензия

AGPL-3.0. См. `LICENSE.txt`.
