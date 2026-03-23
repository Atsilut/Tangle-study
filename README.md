# Tangle

## Overview

**Tangle** is a personal study project focused on designing and implementing a scalable, real-time distributed system.
The goal of this project is not commercialization, but to explore practical backend architecture, real-time communication, and DevOps patterns in a production-like environment.

This project combines multiple technologies and languages to simulate a modern service architecture, including API servers, real-time messaging, background workers, and monitoring systems.

---

## Objectives

* Build a **real-time backend system** with chat, social features, and location sharing
* Practice **distributed system design**
* Explore **multi-language architecture** (C#, Rust, Go)
* Implement **DevOps workflows** (CI/CD, containerization, observability)
* Understand trade-offs between **performance, complexity, and scalability**

---

## Core Features

* Community platform (posts, comments, nested replies)
* Friend and group management
* Real-time chat (1:1 and group)
* Media sharing (images/videos via posts and chat)
* Memory Map (location-based content visualization)
* Real-time location sharing with safety alerts

---

## Architecture

### High-Level Structure

```
Clients
 ├─ Web (React)
 └─ Mobile (MAUI)
        │
        ▼
ASP.NET Core API (Gateway)
        │
 ┌──────┼──────────────┐
 ▼      ▼              ▼
DB    Redis        Queue (Streams)
         │              │
         ▼              ▼
     SignalR       Rust Workers
         │
         ▼
   Real-time Events

Monitoring:
API / Workers → Prometheus → Grafana
```

---

## Tech Stack

### Backend

* **ASP.NET Core**

  * Main API server
  * Handles authentication, business logic, and routing

* **PostgreSQL**

  * Primary relational database

---

### Real-Time & Caching

* **Redis**

  * Caching layer
  * Pub/Sub for real-time messaging
  * Streams for lightweight queueing
  * TTL-based storage for location data

* **SignalR**

  * Real-time communication (chat, location updates)

---

### Workers

* **Rust**

  * High-performance background processing
  * Media processing (image/video)
  * Event handling and aggregation
  * Location clustering for Memory Map

---

### DevOps & Tooling

* **Docker**

  * Containerized services

* **GitHub Actions**

  * CI/CD pipeline

* **Go**

  * CLI utilities
  * Load testing tools
  * Custom exporters (if needed)

---

### Monitoring

* **Prometheus**

  * Metrics collection

* **Grafana**

  * Visualization dashboards

---

## Design Decisions

### Monorepo Structure

All services, workers, and tools are managed in a single repository for clarity

```
/services
  /api
/clients
  /web
/workers
/libs
/tools
/infra
```

---

### Technology Separation by Responsibility

* **C# (ASP.NET Core)** → API and business logic
* **Rust** → Performance-critical and asynchronous processing
* **Go** → DevOps tooling
* React -> Frontend

---

### About Message Queue

Redis will be used as:

* Cache
* Real-time messaging backbone
* Lightweight queue (Streams)

To reduce premature complexity while still supporting scalability.

But Kafka or other systems can be introduced later if needed.

---

### Event-Driven Processing

Expected heavy or asynchronous tasks,

```
API → Queue → Rust Worker → Result Storage
```

This improves responsiveness and system scalability.

---

### Monitoring

* Prometheus
* Grafana

---

## Development Phases

1. Core API (auth, community, friends)
2. Real-time chat (SignalR)
3. Redis integration (cache + pub/sub)
4. Rust workers (media processing)
5. Location features (Memory Map)
6. Monitoring setup
7. Optional client (MAUI)

---

## Disclaimer

This project is built **for learning purposes only**.

* Not optimized for production use
* May include experimental design decisions
* Focused on exploring architecture rather than delivering a finished product

---

## Future Considerations

* Replace Redis Streams with Kafka (if scaling demands it)
* Introduce service decomposition (microservices)
* Add distributed tracing (e.g., OpenTelemetry)
* Improve fault tolerance and recovery strategies

---

## Summary

Tangle is an exploration of:

* Real-time systems
* Distributed architecture
* Multi-language backend design
* Practical DevOps workflows

The project prioritizes **learning depth and architectural clarity** over completeness or production readiness.
