# Troubleshooting and FAQ

Common issues and solutions when using the TOYOPUC Computer Link .NET library.

## 1. Connection Issues

### Unable to connect to Port 1025
- **Check PLC Settings**: Ensure the "Computer Link" service is enabled in the PLC configuration (e.g., via Toyopuc Manager).
- **Firewall**: Ensure the Windows Firewall or any network switches are not blocking TCP/UDP port 1025.
- **Station No.**: Verify the station number matches the PLC's hardware switch or software setting.

## 2. Communication Errors

### Error Code 0x40 (Invalid Address)
- The requested device is outside the range supported by your specific TOYOPUC model. Refer to [Model Ranges](../user/MODEL_RANGES.md) for details.

### Timeout Exceptions
- Check network latency. Consider increasing the `Timeout` property in `ToyopucDeviceClient`.

## 3. General Questions

### How many points can I read at once?
The library automatically splits large requests according to the protocol limits (approx. 960 words per frame for standard TOYOPUC).

### Does this work with Toyopuc-Plus?
Yes, it is fully tested and verified with TOYOPUC-Plus and Nano 10GX series.
