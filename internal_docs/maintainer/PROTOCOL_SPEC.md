# TOYOPUC Computer Link Protocol Spec (.NET)

This document is a working protocol summary for the TOYOPUC 2ET Ethernet module.

It is not a verbatim manufacturer manual. It is a reorganized implementation note based on the current code and verified hardware behavior.

Related documents:

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [TESTING_GUIDE.md](TESTING_GUIDE.md)
- [VALIDATION.md](VALIDATION.md)

Status labels:

- `verified`: behavior confirmed on current hardware
- `implementation rule`: behavior intentionally used by this project
- `summary`: reorganized description for easier implementation

## 1. Frame Format

Common command frame:

```text
+------+------+------+------+------+-------------------+
| FT   | RC   | LL   | LH   | CMD  | DATA              |
+------+------+------+------+------+-------------------+
```

- `FT`: frame type
- `RC`: response code
- `LL/LH`: payload length (little-endian), excluding the 5-byte header
- `CMD`: command code
- `DATA`: command payload

### FT / RC

- request: `FT=0x00`, `RC=0x00`
- response: `FT=0x80`, `RC=0x00` on success
- `RC != 0x00` means PLC-side error or rejection

### Error Response Codes

If the PLC returns a NAK response, the detailed error code may appear:

- in the `CMD` byte, for example `80 10 01 00 40`
- or in the response data, depending on command path

`RC=0x10` is treated as NAK; the client tries both forms when formatting the error.

| Code | Meaning |
| --- | --- |
| `0x11` | CPU module hardware failure |
| `0x20` | Relay command ENQ fixed data is not `0x05` |
| `0x21` | Invalid transfer number in relay command |
| `0x23` | Invalid command code |
| `0x24` | Invalid subcommand code |
| `0x25` | Invalid command-format data byte |
| `0x26` | Invalid number of data points |
| `0x27` | Address out of range |
| `0x28` | Data value out of range |
| `0x40` | Address out of range (alternate form) |

## 2. Command Groups

### Basic Area (CMD=1C–25)

| CMD | Name | Notes |
| --- | --- | --- |
| `0x1C` | `ReadWords` | read N contiguous words |
| `0x1D` | `WriteWords` | write N contiguous words |
| `0x1E` | `ReadBytes` | read N contiguous bytes |
| `0x1F` | `WriteBytes` | write N contiguous bytes |
| `0x20` | `ReadBit` | read 1 bit |
| `0x21` | `WriteBit` | write 1 bit |
| `0x22` | `ReadWordsMulti` | read non-contiguous words |
| `0x23` | `WriteWordsMulti` | write non-contiguous words |
| `0x24` | `ReadBytesMulti` | read non-contiguous bytes |
| `0x25` | `WriteBytesMulti` | write non-contiguous bytes |

### Clock / Status (CMD=32, CMD=A0)

Sub-command structure: `CMD=32`, `SUB_HI`, `SUB_LO`

| Sub | Name |
| --- | --- |
| `0x70 0x00` | `ReadClock` |
| `0x71 0x00` | `WriteClock` |
| `0x11 0x00` | `ReadCpuStatus` |
| `CMD=0xA0`, data `0x01 0x10` | `ReadCpuStatusA0` |

### Extended Area (CMD=94–99)

| CMD | Name | Notes |
| --- | --- | --- |
| `0x94` | `ReadExtWords` | read extended-area words |
| `0x95` | `WriteExtWords` | write extended-area words |
| `0x96` | `ReadExtBytes` | read extended-area bytes |
| `0x97` | `WriteExtBytes` | write extended-area bytes |
| `0x98` | `ReadExtMulti` | multi-point extended read |
| `0x99` | `WriteExtMulti` | multi-point extended write |

### PC10 Commands (CMD=C2–C5)

| CMD | Name | Notes |
| --- | --- | --- |
| `0xC2` | `Pc10BlockRead` | 32-bit address block read |
| `0xC3` | `Pc10BlockWrite` | 32-bit address block write |
| `0xC4` | `Pc10MultiRead` | multi-point PC10 read |
| `0xC5` | `Pc10MultiWrite` | multi-point PC10 write |

PC10 addressing uses a 32-bit address: low word first, then high word.

FR access uses PC10 block commands with `Ex No. = 0x40–0x7F`.

### FR Register (CMD=CA)

- `CMD=CA`, payload: `[Ex No.]`
- Commits a previously written FR block to non-volatile storage.

### Relay Command (CMD=60)

Single outer frame wraps an inner command frame.

```text
[ENQ=05] [link] [exchange_lo] [exchange_hi] [inner_frame]
```

Nested relay is supported by wrapping additional relay frames.

## 3. Address Tables

### Basic Area

| Area | Address Range |
| --- | --- |
| P (word) | 0x0000 – varies by model |
| L (word) | 0x0000 – varies by model |

### Extended Area (Ex No.)

| Ex No. | Area |
| --- | --- |
| 0x00–0x3F | Extended I/O |
| 0x40–0x7F | FR area (via PC10) |
| 0x80–0xFF | Manufacturer reserved |

## 4. Implementation Notes

### Word Endianness

All multi-byte values are little-endian (low byte first).

### 32-Bit Values

32-bit values span two consecutive 16-bit words: low word first.
`ToyopucClient.ReadDWords` / `WriteDWords` handle this packing automatically.

### BCD Clock Encoding

`ReadClock` / `WriteClock` values use BCD encoding for year, month, day, hour, minute, second.
The .NET library converts automatically between `DateTime` and BCD.
