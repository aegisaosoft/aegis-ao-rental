# CarRental.Tests

Тестовый проект для Aegis AO Rental System.

## 📋 Структура тестов

```
CarRental.Tests/
├── Models/
│   ├── BookingTests.cs         # Тесты модели Booking
│   ├── CustomerTests.cs        # Тесты модели Customer
│   ├── VehicleTests.cs         # Тесты модели Vehicle
│   └── EnumTests.cs            # Тесты перечислений и статусов
├── Helpers/
│   └── CurrencyHelperTests.cs  # Тесты CurrencyHelper
├── BusinessLogic/
│   └── BookingCalculationTests.cs # Тесты бизнес-логики расчётов
├── Validation/
│   └── ModelValidationTests.cs # Тесты валидации моделей
├── Stripe/
│   ├── WebhookHandlerTests.cs    # Тесты обработчиков Stripe webhooks
│   ├── SecurityDepositTests.cs   # Тесты авторизации/захвата/возврата депозита
│   ├── StripeConnectTests.cs     # Тесты Stripe Connect аккаунтов
│   └── PaymentProcessingTests.cs # Тесты Checkout Session, Payment Intent, Refund
├── Meta/
│   ├── MetaOAuthServiceTests.cs  # Тесты Meta OAuth (Facebook/Instagram)
│   └── InstagramCampaignTests.cs # Тесты Instagram публикации и кампаний
├── Integration/
│   ├── BookingIntegrationTests.cs # Интеграционные тесты с PostgreSQL
│   └── PostgreSqlCollection.cs    # xUnit collection для PostgreSQL
└── Infrastructure/
    └── PostgresTestBase.cs     # Базовый класс с Azure PostgreSQL
```

## 🚀 Запуск тестов

### Требования
- .NET 9.0 SDK
- **Azure PostgreSQL** (для интеграционных тестов)

### Настройка подключения к Azure PostgreSQL

**Вариант 1: Переменная окружения**
```powershell
$env:TEST_DATABASE_CONNECTION_STRING = "Host=YOUR_SERVER.postgres.database.azure.com;Database=carrental_test;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

**Вариант 2: appsettings.Test.json**
Отредактируй файл `CarRental.Tests/appsettings.Test.json`:
```json
{
  "ConnectionStrings": {
    "TestDatabase": "Host=YOUR_SERVER.postgres.database.azure.com;Database=carrental_test;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

### Все тесты
```bash
dotnet test
```

### С подробным выводом
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Конкретная категория
```bash
dotnet test --filter "FullyQualifiedName~Models"
dotnet test --filter "FullyQualifiedName~Integration"
dotnet test --filter "FullyQualifiedName~BusinessLogic"
dotnet test --filter "FullyQualifiedName~Stripe"
dotnet test --filter "FullyQualifiedName~Meta"
```

### С покрытием кода
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 📦 Используемые пакеты

- **xUnit** - тестовый фреймворк
- **FluentAssertions** - читаемые assertions
- **Moq** - мок-объекты
- **AutoFixture** - генерация тестовых данных
- **Npgsql.EntityFrameworkCore.PostgreSQL** - EF Core провайдер для PostgreSQL
- **Microsoft.Extensions.Configuration** - конфигурация из файлов и переменных окружения

## ✅ Покрытие тестами

### Модели
- ✅ Booking - инициализация, расчёты, статусы, Stripe интеграция
- ✅ Customer - роли, адреса, валидация
- ✅ Vehicle - статусы, VIN, особенности
- ✅ Enums - VehicleStatus, CustomerType, статусные переходы

### Бизнес-логика
- ✅ Расчёт стоимости аренды
- ✅ Налоговые расчёты
- ✅ Платформенные комиссии
- ✅ Депозитная логика
- ✅ Конвертация валют для Stripe

### Валидация
- ✅ Email формат
- ✅ Обязательные поля
- ✅ Длина строк
- ✅ Формат времени

### Helpers
- ✅ CurrencyHelper - валюты по странам

### Интеграционные (Azure PostgreSQL)
- ✅ CRUD операции с Booking
- ✅ Navigation properties
- ✅ Фильтрация по статусам
- ✅ Агрегация выручки
- ✅ JSONB поля

### Stripe
- ✅ Webhook handlers (payment_intent, charge, checkout.session, account)
- ✅ Security Deposit (authorize, capture, release)
- ✅ Stripe Connect (onboarding, status, transfers, payouts)
- ✅ Payment processing (checkout, payment intent, refund)
- ✅ Currency conversion (zero-decimal currencies)
- ✅ Locale handling (pt-BR, es-419, etc.)

### Meta (Facebook/Instagram)
- ✅ OAuth state generation and validation
- ✅ Meta credentials storage (tokens, page selection)
- ✅ Token expiration and refresh tracking
- ✅ Instagram account linking
- ✅ Auto-publish settings
- ✅ Deep link configuration
- ✅ Caption generation with emojis
- ✅ Hashtag recommendations (brand, category, location)
- ✅ Scheduled posts (create, cancel, status transitions)
- ✅ Vehicle social posts tracking
- ✅ Carousel posts validation (2-10 items)
- ✅ Post analytics metrics
- ✅ Auto-post trigger settings

## 📝 Примечания

- Юнит-тесты не требуют базы данных
- Интеграционные тесты подключаются к Azure PostgreSQL
- Тестовые данные автоматически очищаются после каждого теста
- Каждый тест использует уникальные идентификаторы для изоляции
- `PostgresTestBase` предоставляет методы для seed-данных
