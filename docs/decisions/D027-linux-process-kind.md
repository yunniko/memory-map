# D027 · A Linux process's kind comes from its name, cgroup and account
Date: 2026-10-12 · Goal: G-003 M1 · Status: active (superseded by: —)
Context: The Linux groups (apps, system services, VMs and containers) need a rule that needs no root and no desktop-session API.
Decision: VMs and containers: the image name starts with a hypervisor prefix (QEMU, VirtualBox, VMware, Firecracker, crosvm, cloud-hypervisor) or the cgroup names Docker, containerd, Podman, Kubernetes, LXC or machine.slice. Otherwise App for uid 1000–59999, else Service. Kernel threads are left out.
Force: judgment — the uid range follows login.defs defaults on Debian, Ubuntu, Fedora and Arch; the cgroup names are the runtimes' own.
Rejected: the systemd unit or slice alone (WSL puts every process in /init.scope); the controlling terminal (GUI apps have none).
Consequence: A daemon run under a regular account counts as an app. Name and kind are cached per (pid, start time), so a reused pid is identified again.
Evidence: src/MemoryMap.Linux/LinuxMemorySampler.cs; tests/MemoryMap.Linux.Tests/LinuxMemorySamplerTests.cs
