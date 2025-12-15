# Wex Corporate Payments API

A robust, enterprise-grade **RESTful API** built with **.NET 10** to manage corporate purchase transactions. It features **real-time currency conversion** using the *US Treasury Reporting Rates of Exchange API*, strict validation rules, and **idempotency** for transaction safety.

---

## 🚀 Key Features

- **Purchase Management**  
  Securely store transaction details (*Description, Date, Amount, Currency*).

- **Currency Conversion**  
  Integrates with the US Treasury API to convert purchase amounts to a target currency.

- **Smart Rate Validation**  
  Enforces logic to find exchange rates within a **6-month window** prior to the purchase date.

- **Idempotency**  
  Implements an `Idempotency-Key` header mechanism to prevent duplicate transactions (double-spending protection).

- **Safe Retry**  
  Returns the original response when the request is retried with identical data.

- **Conflict Detection**  
  Returns **HTTP 409 Conflict** if the same idempotency key is reused with different data.

- **Resilience**  
  Architecture designed for fault tolerance and clear, explicit error reporting.

---

## 🛠️ Technology Stack

- **Framework:** .NET 10 (ASP.NET Core Web API)
- **Language:** C# 13
- **Architecture:** Clean Architecture (DDD principles)
- **Database:** SQL Server (Entity Framework Core)
- **Testing:** xUnit, Moq, FluentAssertions
- **Documentation:** Swagger / OpenAPI
- **Containerization:** Docker & Docker Compose

---

## 🏗️ Architecture Overview

The solution follows the **Clean Architecture** pattern to ensure separation of concerns and high testability:

- **Domain**  
  Core entities (`Purchase`), value objects (`Money`), and business exceptions. No external dependencies.

- **Application**  
  Use cases (`StorePurchaseTransaction`), DTOs, and interfaces. Orchestrates the business flow.

- **Infrastructure**  
  Implementations of interfaces (SQL repositories, `TreasuryApiClient`, EF Core context).

- **API**  
  Entry point including controllers, middleware, and global exception handling.

---

## 📋 Prerequisites

- .NET 10 SDK
- Docker Desktop (optional, for containerized execution)
- SQL Server (required when running locally without Docker)

---

## 🏃‍♂️ Getting Started

### Option 1: Run with Docker Compose (Recommended)

This is the fastest way to start. It spins up both the API and a SQL Server container automatically.

```bash
docker-compose up \--build
```

The API will be available at:

```
http://localhost:7000/swagger
```

---

### Option 2: Run Locally (Manual)

#### Apply Migrations

```bash
cd Wex.CorporatePayments.Infrastructure
dotnet ef database update \--startup-project ../Wex.CorporatePayments.Api
```

#### Run the API

```bash
cd Wex.CorporatePayments.Api
dotnet run
```

---

## 🧪 Testing

```bash
dotnet test
dotnet test \--logger "console;verbosity=detailed"
```

---

## 📦 Sample Request — Create Purchase

```http
POST /api/purchases
Idempotency-Key: <GUID>
```

```json
{
  "description": "Office Supplies",
  "transactionDate": "2023-12-15T14:30:00Z",
  "amount": 150.00,
  "currency": "USD"
}
```
