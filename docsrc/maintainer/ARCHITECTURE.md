# Project Architecture and Design Goals

This document outlines the core design philosophy and technical evolution of the TOYOPUC Computer Link .NET library.

## 1. Project Background
Originally developed to provide a modern, reliable, and high-performance alternative to legacy communication libraries. The goal was to leverage .NET's Task-based Asynchronous Pattern (TAP) for efficient industrial monitoring.

## 2. Core Design Principles
- **Async First**: All I/O operations are inherently asynchronous to prevent thread-pool starvation in large-scale monitoring systems.
- **Provider Independence**: Separation of protocol framing logic from the transport layer (TCP/UDP).
- **Strict Typing**: Using .NET's type system to prevent common PLC addressing errors at compile-time.

## 3. Implementation Details
The library uses a tiered approach:
- **Low-Level Client**: Direct frame manipulation.
- **High-Level Client**: Human-readable device strings (e.g., "P1-D100") and automatic range validation.
- **Catalog System**: Centrally managed device range profiles based on official JTEKT specifications.
