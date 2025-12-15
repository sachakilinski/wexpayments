# Challenge Implementation: Approach & Decisions

This document outlines the thought process, architectural decisions, and strategies used to solve the **Wex Corporate Payments API** challenge.

---

## 🗺️ The Plan

My approach was divided into clear phases to ensure correctness, scalability, and maintainability:

1. **Domain Modeling**  
   Defined the core `Purchase` entity and `Money` value object first.  
   Validation rules (no negative amounts, no future dates) live in the **Domain layer**, not in controllers, ensuring business invariants are always enforced.

2. **Clean Architecture Setup**  
   Established a strict project structure:

   ```
   Domain -> Application -> Infrastructure -> API
   ```

   This decouples dependencies, improves testability, and prevents framework leakage into the business logic.

3. **Treasury Integration**  
   Implemented the `TreasuryApiClient` to consume the US Treasury Reporting Rates API, focusing on:
   - Correct query string construction
   - Enforcing the **6-month historical window** requirement
   - Clear error handling when rates are missing or invalid

4. **Idempotency Implementation**  
   Designed a safe retry mechanism to protect against duplicate transactions caused by network retries.

5. **Refactoring & Testing**  
   Moved from happy-path implementation to edge-case handling, adding:
   - Unit tests with **xUnit** and **Moq**
   - Manual Postman scenarios to validate real-world request flows

---

## ⚡ Concurrency & Idempotency

One of the most critical requirements was guaranteeing **exactly-once processing** in a concurrent, distributed environment.

### The Problem

Clients may retry requests due to network timeouts or transient failures.  
Without idempotency, retries could result in **double charges**.

### The Solution

I implemented an **Idempotency Key** strategy:

- **Mechanism**  
  Clients send a unique `Idempotency-Key` header with each request.

- **Logic**
  1. **Scenario I-01 — Safe Retry**  
     If the key already exists **and** the request payload (amount, date, description, currency) matches the original request, the API returns the **original success response** (`200` or `201`).  
     This makes retries safe and transparent.

  2. **Scenario I-02 — Conflict**  
     If the key exists **but** the payload differs, the API returns **`409 Conflict`**, preventing accidental or malicious reuse of idempotency keys.

- **Database Enforcement**  
  A **unique constraint** on the `IdempotencyKey` column guarantees correctness even when two identical requests hit the server simultaneously, protecting against race conditions at the persistence layer.

---

## 💾 Caching Strategy

To optimize performance and reduce external dependency load, I planned and implemented caching for exchange rates.

- **Why**  
  Historical exchange rates are immutable. Calling the Treasury API for every request introduces unnecessary latency and cost.

- **Implementation**  
  Used `IMemoryCache` to store exchange rates retrieved from the Treasury API.

- **Cache Key**

  ```
  Currency_Date
  ```

  Example:

  ```
  BRL_2023-12-15
  ```

- **TTL (Time To Live)**  
  - Short TTL (for example, 60 minutes) for safety  
  - Effectively infinite for historical dates, since past rates never change

---

## 🧪 Testing Strategy

I followed the **Testing Pyramid** approach:

### 1. Unit Tests

- Focused on the **Application layer** (Use Cases)
- Validated business rules such as:
  - Exchange rates must fall within the 6-month window
  - Invalid amounts or dates are rejected
- Tested the **Infrastructure layer**:
  - Treasury API URL construction
  - Response parsing and error handling

### 2. Manual Validation (Postman)

- Created a Postman collection to simulate real client behavior
- Explicitly tested idempotency flows:
  - Scenario I-01: Safe retry
  - Scenario I-02: Conflict on reused key with different payload
- Covered edge cases that are difficult to fully mock in unit tests

### 3. Mocking External Dependencies

- Mocked the `TreasuryApiClient` to simulate:
  - External API downtime
  - Missing exchange rates
  - Valid and invalid responses

---

## 🚀 Future Improvements

Given more time, the following enhancements would further harden the system:

- **Resilience Policies (Polly)**  
  Add circuit breakers, timeouts, and exponential backoff for Treasury API calls.

- **Distributed Caching (Redis)**  
  Replace `IMemoryCache` to support horizontal scaling without cache inconsistency.

- **Background Processing**  
  Move transaction persistence or currency conversion to background queues  
  (for example, MassTransit or RabbitMQ) for high-throughput, eventually consistent scenarios.

---
