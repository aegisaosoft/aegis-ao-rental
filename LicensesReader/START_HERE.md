# 📥 Driver License Scanner - Download Instructions

## ✅ Чистые файлы готовы к использованию

Все файлы созданы с нуля без исправлений старых версий.

## 📦 Структура проекта

```
license-scanner-complete/
├── backend/
│   ├── LicenseService.cs         - Сервис для работы с лицензиями
│   └── LicenseController.cs      - API контроллер
│
├── database/
│   └── 001_license_scanning.sql  - Миграция базы данных
│
├── mobile/
│   ├── DriverLicenseScanner.tsx  - Компонент сканера
│   ├── App.tsx                   - Пример использования
│   ├── package.json              - Зависимости
│   ├── android/
│   │   └── app/src/main/AndroidManifest.xml
│   └── ios/
│       ├── Podfile
│       └── Info.plist.additions
│
└── docs/
    └── README.md                 - Полная документация
```

## 🚀 Быстрый старт

### 1. База данных
```bash
psql -h your-db.postgres.database.azure.com \
  -U your-username \
  -d your-database \
  -f database/001_license_scanning.sql
```

### 2. Backend (.NET)
```csharp
// В Program.cs
builder.Services.AddScoped<ILicenseService, LicenseService>();

// Скопировать файлы:
// backend/LicenseService.cs → YourProject/Services/
// backend/LicenseController.cs → YourProject/Controllers/
```

### 3. Mobile App
```bash
cd mobile
npm install
cd ios && pod install && cd ..

# Запуск
npx react-native run-ios
# или
npx react-native run-android
```

## ⚠️ Важные особенности

### Адреса хранятся раздельно
```
customers.address              → ТЕКУЩИЙ адрес (биллинг, доставка)
customer_licenses.license_address → Адрес НА ЛИЦЕНЗИИ (часто устаревший)
```

### Что синхронизируется
✅ Синхронизируется (если пусто):
- first_name
- last_name  
- date_of_birth

❌ НЕ синхронизируется:
- address (используется отдельное поле license_address)
- city, state, postal_code
- phone, email

### Валидация
- Минимальный возраст: 21 год
- Проверка просрочки лицензии
- Дубликаты в рамках компании не допускаются

## 📁 Скачать файлы

### Полный архив (рекомендуется)
[Download Complete Package (license-scanner-complete.tar.gz)](computer:///mnt/user-data/outputs/license-scanner-complete.tar.gz)

Включает все файлы в правильной структуре директорий.

### Отдельные файлы

**Backend:**
- [LicenseService.cs](computer:///mnt/user-data/outputs/backend/LicenseService.cs)
- [LicenseController.cs](computer:///mnt/user-data/outputs/backend/LicenseController.cs)

**Database:**
- [001_license_scanning.sql](computer:///mnt/user-data/outputs/database/001_license_scanning.sql)

**Mobile:**
- [DriverLicenseScanner.tsx](computer:///mnt/user-data/outputs/mobile/DriverLicenseScanner.tsx)
- [App.tsx](computer:///mnt/user-data/outputs/mobile/App.tsx)
- [package.json](computer:///mnt/user-data/outputs/mobile/package.json)
- [AndroidManifest.xml](computer:///mnt/user-data/outputs/mobile/android/app/src/main/AndroidManifest.xml)
- [Podfile](computer:///mnt/user-data/outputs/mobile/ios/Podfile)
- [Info.plist.additions](computer:///mnt/user-data/outputs/mobile/ios/Info.plist.additions)

**Documentation:**
- [README.md](computer:///mnt/user-data/outputs/docs/README.md)

## 🔧 API Endpoints

### Сканировать лицензию
```http
POST /api/license/scan
{
  "companyId": "guid",
  "customerId": "guid",  ← Существующий customer
  "licenseData": {
    "firstName": "John",
    "lastName": "Doe",
    "licenseNumber": "D1234567",
    "state": "CA",
    "dateOfBirth": "01/15/1990",
    "expirationDate": "01/15/2026",
    "address": "123 Main St",  ← Сохранится в license_address
    ...
  },
  "syncCustomerData": true  ← Только имя и DOB, НЕ адрес
}
```

### Получить лицензию клиента
```http
GET /api/license/customer/{customerId}
```

### Просроченные лицензии
```http
GET /api/license/company/{companyId}/expired
```

### Клиенты без лицензии
```http
GET /api/license/company/{companyId}/without-license
```

### Проверить доступность лицензии
```http
GET /api/license/company/{companyId}/check-license
  ?licenseNumber=D1234567&state=CA
```

## 📊 Database Views

```sql
-- Полные данные клиента с лицензией
SELECT * FROM v_customers_complete WHERE customer_id = 'xxx';

-- Просроченные лицензии
SELECT * FROM v_expired_licenses WHERE company_id = 'xxx';

-- Клиенты без лицензии
SELECT * FROM v_customers_without_license WHERE company_id = 'xxx';

-- Последние сканирования
SELECT * FROM v_recent_scans WHERE company_id = 'xxx' LIMIT 10;
```

## ✅ Проверка установки

### После миграции БД
```sql
-- Проверить таблицы
SELECT table_name FROM information_schema.tables 
WHERE table_name IN ('customer_licenses', 'license_scans');

-- Проверить views
SELECT table_name FROM information_schema.views 
WHERE table_name LIKE '%customer%';
```

### Тестовый запрос
```bash
curl -X POST http://localhost:5000/api/license/scan \
  -H "Content-Type: application/json" \
  -d @test_scan.json
```

## 🛡️ Security

### Row Level Security включен
```sql
SET app.current_company_id = 'your-company-guid';
-- Все запросы автоматически фильтруются по компании
```

### Добавить аутентификацию
```csharp
[Authorize]
[HttpPost("scan")]
public async Task<ActionResult> ScanLicense(...)
{
    var companyId = Guid.Parse(User.FindFirst("CompanyId")?.Value);
    // ...
}
```

## 🐛 Troubleshooting

### "Index already exists"
Запустите сначала:
```bash
psql -f database/rollback_license_scanning.sql
```

### Camera не работает
Проверьте permissions в AndroidManifest.xml и Info.plist

### Лицензия не парсится
Убедитесь что сканируете PDF417 barcode на ОБРАТНОЙ стороне лицензии

## 📖 Дополнительная документация

- **docs/README.md** - Полная документация системы
- **ADDRESS_HANDLING.md** - Работа с адресами (ВАЖНО!)
- **MIGRATION_GUIDE.md** - Решение проблем с миграцией

---

**Версия:** 1.0.0  
**Дата:** 29 октября 2025  
**Совместимость:** PostgreSQL 13+, .NET 8+, React Native 0.73+
