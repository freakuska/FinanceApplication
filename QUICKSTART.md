# Быстрый старт FinanceApp

## Подготовка

1. Убедитесь, что PostgreSQL запущен
2. Скопируйте `FinanceApp.Api/appsettings.Development.example.json` в `FinanceApp.Api/appsettings.Development.json`
3. Проверьте строку подключения и JWT SecretKey в локальном `FinanceApp.Api/appsettings.Development.json`

## Запуск (в 3 простых шага)

### 1. Применить миграции БД

```bash
cd FinanceApp.Infrastructure
dotnet ef database update --startup-project ../FinanceApp.Api
cd ..
```

### 2. Запустить API (в первом терминале)

```bash
cd FinanceApp.Api
dotnet run
```

✅ API доступен: https://localhost:7051
✅ Swagger UI: https://localhost:7051/swagger

### 3. Запустить Web (во втором терминале)

```bash
cd FinanceApp.Web
dotnet run
```

✅ Веб-приложение: https://localhost:7073

## Первый вход

1. Откройте https://localhost:7073
2. Будете перенаправлены на страницу логина
3. Нажмите "Зарегистрироваться"
4. Заполните форму регистрации
5. После регистрации автоматически попадете на дашборд

## Что можно делать?

### На дашборде (/Dashboard):
- ✅ Просмотр статистики (доходы, расходы, баланс)
- ✅ Добавление новых операций
- ✅ Просмотр списка операций
- ✅ Удаление операций

### На странице тегов (/Tags):
- ✅ Просмотр тегов по типам (Доходы, Расходы, Переводы)
- ✅ Создание новых тегов с цветами и иконками
- ✅ Удаление тегов

## Тестовые данные

Вы можете добавить операции через дашборд или использовать Swagger UI для прямого обращения к API:

1. Откройте https://localhost:7051/swagger
2. Зарегистрируйтесь через `/api/auth/register`
3. Скопируйте токен из ответа (хотя для Swagger это не обязательно, так как токены в cookies)
4. Создайте операции через `/api/operations`

## Возможные проблемы

### База данных не доступна
```
Проверьте: PostgreSQL запущен?
Строка подключения правильная?
```

### Ошибки CORS
```
Убедитесь, что оба проекта запущены на правильных портах:
API: https://localhost:7051
Web: https://localhost:7073
```

### Не работают cookies
```
Убедитесь, что используете HTTPS (не HTTP)
Cookies работают только через HTTPS в production mode
```

## Порты по умолчанию

| Сервис | HTTPS | HTTP |
|--------|-------|------|
| API    | 7051  | 7000 |
| Web    | 7073  | 5260 |

## Структура URL

```
https://localhost:7073/              → Редирект на /Dashboard
https://localhost:7073/Account/Login  → Страница входа
https://localhost:7073/Account/Register → Регистрация
https://localhost:7073/Dashboard      → Главная страница
https://localhost:7073/Tags           → Управление тегами
```

## API Endpoints (примеры)

```bash
# Регистрация
POST https://localhost:7051/api/auth/register
{
  "email": "user@example.com",
  "password": "password123",
  "fullName": "Иван Иванов",
  "phone": "+79001234567"
}

# Вход
POST https://localhost:7051/api/auth/login
{
  "email": "user@example.com",
  "password": "password123"
}

# Создание операции
POST https://localhost:7051/api/operations
{
  "type": 0,  // 0 = Income, 1 = Expense
  "amount": 5000,
  "currency": "RUB",
  "paymentMethod": 1,  // 0 = Cash, 1 = Card, 2 = BankTransfer
  "description": "Зарплата",
  "operationDateTime": "2024-10-24T12:00:00Z",
  "tagIds": []
}

# Получить статистику
GET https://localhost:7051/api/operations/stats?startDate=2024-10-01&endDate=2024-10-31

# Создать тег
POST https://localhost:7051/api/tags
{
  "name": "Продукты",
  "type": 1,  // 0 = Income, 1 = Expense, 2 = Transfer
  "color": "#ff6b6b",
  "icon": "🛒",
  "visibility": "Private"
}
```

## Следующие шаги

После успешного запуска вы можете:

1. Добавить несколько операций разных типов
2. Создать теги для категоризации
3. Посмотреть статистику на дашборде
4. Изучить Swagger UI для понимания всех доступных API методов

## Полезные ссылки

- [Полная документация](README.md)
- [Swagger UI](https://localhost:7051/swagger) - после запуска API
- [Веб-приложение](https://localhost:7073) - после запуска Web

## Нужна помощь?

Проверьте логи в консоли где запущены проекты - там будут видны все ошибки.

