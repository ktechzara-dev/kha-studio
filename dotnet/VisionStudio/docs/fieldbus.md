# Industrial Fieldbus Integration Guide (PROFINET & EtherNet/IP)

## 1. Overview and Legal/Architectural Boundary
VisionStudio supports direct explicit messaging with industrial PLCs out of the box (Siemens S7-300/400/1200/1500 via TCP RFC 1006, and Modbus TCP).

**Implicit Cyclic I/O (Real-time Class 1 / RT):**
- **PROFINET RT / IRT** and **EtherNet/IP Class 1 Implicit I/O** require certified real-time operating system kernel drivers, dedicated hardware MAC controllers, or proprietary vendor stacks (such as Siemens CP 1616 / CP 1626, Hilscher CIFX, or licensed commercial stacks like Real-Time Systems / Molex).
- In accordance with legal boundaries and the "no fake stubs" rule, VisionStudio **does not include uncertified fake stubs**. Instead, this document provides the exact industrial architecture for connecting VisionStudio to cyclic Fieldbus networks.

---

## 2. Production Deployment Patterns

### Pattern A: Gateway Coupler (Recommended for Factory Floors)
Connect a certified industrial Gateway Coupler between the Fieldbus network and VisionStudio:
- **PROFINET to Modbus TCP / OPC UA Gateway** (e.g., Anybus X-gateway, HMS Communicator, or Siemens PN/PN Coupler).
- **EtherNet/IP to Modbus TCP Gateway** (e.g., ProSoft Technology, Moxa MGate).
- **How it works:**
  1. The PLC configures cyclic I/O bytes to the gateway via standard GSDML / EDS configuration in TIA Portal or Studio 5000.
  2. The gateway maps these I/O registers to Modbus TCP Holding Registers or OPC UA nodes.
  3. VisionStudio reads/writes results, trigger signals, and recipe numbers through `VisionStudio.Devices.Plc` or `VisionStudio.Connectivity.OpcUa` at sub-millisecond speeds.

### Pattern B: Dedicated PCI Express / M.2 Fieldbus Interface Cards
For high-speed deterministic line rates (< 2 ms cyclic jitter):
- Install a hardware coprocessor card such as **Hilscher cifX 50-RE** (or cifX M.2).
- The card handles all PROFINET / EtherNet/IP protocol handshakes on its onboard ASIC.
- VisionStudio interacts with the card's dual-port memory driver via native C# P/Invoke bindings.

### Pattern C: Explicit Messaging
- **Siemens S7 Protocol**: Supported out of the box via `VisionStudio.Devices.Plc.SiemensPlcDriver`. The PLC triggers inspections by toggling bits in a configured DataBlock (`DB1.DBX0.0`).
- **EtherNet/IP Explicit (CIP Generic)**: Send Unconnected Message Manager (UCMM) Read/Write Tag Service requests directly to ControlLogix / CompactLogix controllers.
