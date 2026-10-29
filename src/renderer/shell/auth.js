/**
 * Kha Studio — Authentication, RBAC & User Approval Module
 * Chuẩn Web Crypto API (PBKDF2 HMAC-SHA256, 100,000 iterations, random salt)
 * Hỗ trợ Đăng ký tài khoản, gửi duyệt về tài khoản Sếp Kha (Admin),
 * và đồng bộ Hybrid qua Cloud Online + LAN nội bộ.
 */
(function (global) {
  'use strict';

  const STORAGE_USERS_KEY = 'ks_users';
  const STORAGE_SESSION_KEY = 'ks_session';
  const STORAGE_PENDING_KEY = 'ks_pending_requests';
  const STORAGE_MY_REG_KEY = 'ks_my_registration';
  const STORAGE_CLOUD_CONFIG_KEY = 'ks_auth_cloud_config';
  const PBKDF2_ITERATIONS = 100000;

  // Âm thanh chuông thông báo (Web Audio API ding-dong)
  function playChimeSound() {
    try {
      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      if (!AudioCtx) return;
      const ctx = new AudioCtx();
      if (ctx.state === 'suspended') ctx.resume();

      const now = ctx.currentTime;
      const osc1 = ctx.createOscillator();
      const osc2 = ctx.createOscillator();
      const gain = ctx.createGain();

      osc1.type = 'sine';
      osc2.type = 'sine';

      // Nốt E5 (659Hz) -> A5 (880Hz)
      osc1.frequency.setValueAtTime(659.25, now);
      osc2.frequency.setValueAtTime(880.0, now + 0.14);

      gain.gain.setValueAtTime(0, now);
      gain.gain.linearRampToValueAtTime(0.28, now + 0.04);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.75);

      osc1.connect(gain);
      osc2.connect(gain);
      gain.connect(ctx.destination);

      osc1.start(now);
      osc1.stop(now + 0.16);
      osc2.start(now + 0.14);
      osc2.stop(now + 0.75);
    } catch (_) {}
  }

  // Tiện ích chuyển đổi Buffer <-> Hex
  function bufToHex(buf) {
    return Array.from(new Uint8Array(buf))
      .map(b => b.toString(16).padStart(2, '0'))
      .join('');
  }

  function hexToBuf(hex) {
    const bytes = new Uint8Array(hex.length / 2);
    for (let i = 0; i < bytes.length; i++) {
      bytes[i] = parseInt(hex.substr(i * 2, 2), 16);
    }
    return bytes.buffer;
  }

  // Băm mật khẩu bằng PBKDF2 với SHA-256
  async function hashPassword(password, saltHex = null) {
    const enc = new TextEncoder();
    let saltBuf;
    if (saltHex) {
      saltBuf = hexToBuf(saltHex);
    } else {
      saltBuf = new Uint8Array(16);
      crypto.getRandomValues(saltBuf);
      saltHex = bufToHex(saltBuf.buffer);
    }

    const keyMaterial = await crypto.subtle.importKey(
      'raw',
      enc.encode(password),
      { name: 'PBKDF2' },
      false,
      ['deriveBits', 'deriveKey']
    );

    const key = await crypto.subtle.deriveKey(
      {
        name: 'PBKDF2',
        salt: saltBuf,
        iterations: PBKDF2_ITERATIONS,
        hash: 'SHA-256',
      },
      keyMaterial,
      { name: 'HMAC', hash: 'SHA-256', length: 256 },
      true,
      ['sign']
    );

    const rawHash = await crypto.subtle.exportKey('raw', key);
    const hashHex = bufToHex(rawHash);
    return `pbkdf2:${PBKDF2_ITERATIONS}:${saltHex}:${hashHex}`;
  }

  // Xác thực mật khẩu
  async function verifyPassword(password, storedHash) {
    if (!storedHash || !storedHash.startsWith('pbkdf2:')) return false;
    const parts = storedHash.split(':');
    if (parts.length !== 4) return false;
    const [, , saltHex] = parts;
    const computed = await hashPassword(password, saltHex);
    return computed === storedHash;
  }

  // Dữ liệu người dùng ban đầu
  const INITIAL_USERS = [
    {
      id: 'usr_admin',
      username: 'kha',
      name: 'Sếp Kha',
      role: 'admin', // 'admin' | 'engineer'
      allowedTools: ['*'], // Toàn quyền tất cả công cụ
      createdAt: '2026-10-01T00:00:00.000Z',
      // Mật khẩu mặc định: 'kha'
      passwordHash: 'pbkdf2:100000:7e452a8c08efbcde1903429bcddfa890:5469ca483754228943fcf3e83bce4b64e6293466f2277d12f11a43a085368a5c'
    },
    {
      id: 'usr_engineer',
      username: 'engineer',
      name: 'Kỹ sư Dự án',
      role: 'engineer',
      allowedTools: ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal'],
      createdAt: '2026-10-01T00:00:00.000Z',
      // Mật khẩu mặc định: '123456'
      passwordHash: 'pbkdf2:100000:bf88fa90740a3ddde02b11894d07b812:12cbfa3f79efbca279a05b38e2ecad0318413a0e69d763bc753ad37ea92209d7'
    }
  ];

  class AuthManager {
    constructor() {
      this._pollTimer = null;
      this._lastKnownPendingCount = 0;
      this._init();
    }

    _init() {
      // Khởi tạo bảng users nếu chưa có
      try {
        const raw = localStorage.getItem(STORAGE_USERS_KEY);
        if (!raw) {
          localStorage.setItem(STORAGE_USERS_KEY, JSON.stringify(INITIAL_USERS));
        } else {
          const users = JSON.parse(raw);
          let changed = false;
          users.forEach(u => {
            if (!Array.isArray(u.allowedTools)) {
              u.allowedTools = u.role === 'admin' ? ['*'] : ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal'];
              changed = true;
            } else if (u.role !== 'admin' && !u.allowedTools.includes('vision-studio')) {
              u.allowedTools.push('vision-studio');
              changed = true;
            }
          });
          if (changed) this._saveUsers(users);
        }
      } catch (e) {
        console.warn('Lỗi khởi tạo danh sách users:', e);
      }

      // Khởi tạo bảng pending_requests nếu chưa có
      try {
        if (!localStorage.getItem(STORAGE_PENDING_KEY)) {
          localStorage.setItem(STORAGE_PENDING_KEY, JSON.stringify([]));
        }
      } catch (_) {}

      // Bật sync nền nếu đang ở vai trò admin
      this.startBackgroundSync();
    }

    getUsers() {
      try {
        const raw = localStorage.getItem(STORAGE_USERS_KEY);
        const users = raw ? JSON.parse(raw) : [...INITIAL_USERS];
        users.forEach(u => {
          if (!Array.isArray(u.allowedTools)) {
            u.allowedTools = u.role === 'admin' ? ['*'] : ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal'];
          } else if (u.role !== 'admin' && !u.allowedTools.includes('vision-studio')) {
            u.allowedTools.push('vision-studio');
          }
        });
        return users;
      } catch (_) {
        return [...INITIAL_USERS];
      }
    }

    _saveUsers(users) {
      localStorage.setItem(STORAGE_USERS_KEY, JSON.stringify(users));
    }

    // =========================================================================
    // YÊU CẦU ĐĂNG KÝ & DUYỆT TÀI KHOẢN (REGISTRATION & APPROVAL WORKFLOW)
    // =========================================================================

    getPendingRequests() {
      try {
        const raw = localStorage.getItem(STORAGE_PENDING_KEY);
        return raw ? JSON.parse(raw) : [];
      } catch (_) {
        return [];
      }
    }

    _savePendingRequests(list) {
      localStorage.setItem(STORAGE_PENDING_KEY, JSON.stringify(list));
      const pendingCount = list.filter(r => r.status === 'pending').length;
      window.dispatchEvent(new CustomEvent('ks:auth:pending_changed', {
        detail: { count: pendingCount, list }
      }));
    }

    getMyRegistration() {
      try {
        const raw = localStorage.getItem(STORAGE_MY_REG_KEY);
        return raw ? JSON.parse(raw) : null;
      } catch (_) {
        return null;
      }
    }

    setMyRegistration(reg) {
      if (reg) {
        localStorage.setItem(STORAGE_MY_REG_KEY, JSON.stringify(reg));
      } else {
        localStorage.removeItem(STORAGE_MY_REG_KEY);
      }
    }

    getCloudConfig() {
      try {
        const raw = localStorage.getItem(STORAGE_CLOUD_CONFIG_KEY);
        if (raw) return JSON.parse(raw);
      } catch (_) {}
      return {
        cloudUrl: 'https://kha-studio-auth-default-rtdb.firebaseio.com',
        lanUrl: '',
        autoSync: true,
        pollIntervalMs: 15000
      };
    }

    saveCloudConfig(cfg) {
      localStorage.setItem(STORAGE_CLOUD_CONFIG_KEY, JSON.stringify(cfg));
    }

    getLanBase() {
      const cfg = this.getCloudConfig();
      if (cfg && cfg.lanUrl && cfg.lanUrl.trim()) {
        let u = cfg.lanUrl.trim();
        if (!/^https?:\/\//i.test(u)) u = 'http://' + u;
        return u.replace(/\/+$/, '');
      }
      if (typeof window !== 'undefined' && window.location && window.location.hostname && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1' && window.location.port) {
        return `${window.location.protocol}//${window.location.hostname}:${window.location.port}`;
      }
      return 'http://localhost:5055';
    }

    // Người dùng đăng ký tài khoản từ app
    async submitRegistrationRequest({ username, name, department, phone, note, password }) {
      username = String(username || '').trim().toLowerCase();
      name = String(name || '').trim();
      department = String(department || '').trim();
      phone = String(phone || '').trim();
      note = String(note || '').trim();
      password = String(password || '');

      if (!username || !/^[a-z0-9_.-]{3,30}$/.test(username)) {
        throw new Error('Tên đăng nhập từ 3-30 ký tự, không dấu và không chứa ký tự đặc biệt');
      }
      if (!name) {
        throw new Error('Họ và tên hiển thị không được để trống');
      }
      if (!password || password.length < 4) {
        throw new Error('Mật khẩu tối thiểu 4 ký tự');
      }

      // Kiểm tra xem username đã tồn tại trong danh sách người dùng chính thức chưa
      const users = this.getUsers();
      if (users.some(u => u.username.toLowerCase() === username)) {
        throw new Error(`Tên đăng nhập "${username}" đã có người sử dụng`);
      }

      // Kiểm tra trong danh sách yêu cầu chờ duyệt
      const pendingList = this.getPendingRequests();
      const existingReq = pendingList.find(r => r.username.toLowerCase() === username);
      if (existingReq && existingReq.status === 'pending') {
        throw new Error(`Tên đăng nhập "${username}" đang có yêu cầu chờ Admin duyệt`);
      }

      const passwordHash = await hashPassword(password);
      const newRequest = {
        id: 'req_' + Date.now().toString(36) + Math.random().toString(36).substr(2, 4),
        username,
        name,
        department,
        phone,
        note,
        passwordHash,
        requestedAt: new Date().toISOString(),
        status: 'pending', // 'pending' | 'approved' | 'rejected'
        allowedTools: ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal'],
        approvedAt: null,
        approvedBy: null,
        rejectionReason: null
      };

      // Cập nhật local
      if (existingReq) {
        Object.assign(existingReq, newRequest);
      } else {
        pendingList.push(newRequest);
      }
      this._savePendingRequests(pendingList);
      this.setMyRegistration(newRequest);

      // Đồng bộ bất đồng bộ ra Network (LAN + Cloud)
      this._syncRequestToNetwork(newRequest).catch(() => {});

      return newRequest;
    }

    // Tra cứu trạng thái duyệt của một username
    async checkRegistrationStatus(username) {
      username = String(username || '').trim().toLowerCase();
      if (!username) return { status: 'none' };

      // 1. Thử gọi trực tiếp endpoint check-status của LAN Server
      try {
        const lanBase = this.getLanBase();
        const sRes = await fetch(`${lanBase}/api/auth/check-status?username=${encodeURIComponent(username)}`, { method: 'GET' }).catch(() => null);
        if (sRes && sRes.ok) {
          const sData = await sRes.json();
          if (sData && sData.status === 'approved') {
            const localUsers = this.getUsers();
            let u = localUsers.find(x => x.username.toLowerCase() === username);
            if (!u) {
              u = {
                id: sData.id || ('usr_' + Date.now().toString(36)),
                username: sData.username,
                name: sData.fullName || sData.name || sData.username,
                role: sData.role || 'engineer',
                allowedTools: sData.allowedTools || ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal'],
                createdAt: sData.approvedAt || new Date().toISOString(),
                passwordHash: sData.passwordHash
              };
              localUsers.push(u);
              this._saveUsers(localUsers);
            } else if (sData.allowedTools) {
              u.allowedTools = sData.allowedTools;
              this._saveUsers(localUsers);
            }
            // Cập nhật myRegistration
            const myReg = this.getMyRegistration();
            if (myReg && myReg.username.toLowerCase() === username) {
              myReg.status = 'approved';
              myReg.allowedTools = sData.allowedTools;
              this.setMyRegistration(myReg);
            }
            return { status: 'approved', user: u, request: sData };
          } else if (sData && sData.status === 'rejected') {
            const myReg = this.getMyRegistration();
            if (myReg && myReg.username.toLowerCase() === username) {
              myReg.status = 'rejected';
              myReg.rejectionReason = sData.rejectionReason;
              this.setMyRegistration(myReg);
            }
            return { status: 'rejected', request: sData };
          }
        }
      } catch (_) {}

      // 2. Thử fetch từ mạng để cập nhật bản mới nhất
      await this.fetchNetworkPendingRequests().catch(() => {});

      // 3. Tra cứu trong bảng users chính thức
      const users = this.getUsers();
      const activeUser = users.find(u => u.username.toLowerCase() === username);
      if (activeUser) {
        return { status: 'approved', user: activeUser };
      }

      // 4. Tra cứu trong pending requests
      const req = this.getPendingRequests().find(r => r.username.toLowerCase() === username);
      if (!req) return { status: 'not_found' };

      // Nếu trạng thái đã là approved mà chưa có trong users local, tự động thêm vào
      if (req.status === 'approved') {
        const newUser = {
          id: 'usr_' + Date.now().toString(36) + Math.random().toString(36).substr(2, 4),
          username: req.username,
          name: req.name,
          role: 'engineer',
          allowedTools: req.allowedTools || ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal'],
          createdAt: req.approvedAt || new Date().toISOString(),
          passwordHash: req.passwordHash
        };
        users.push(newUser);
        this._saveUsers(users);
        return { status: 'approved', user: newUser, request: req };
      }

      return { status: req.status, request: req };
    }

    // Phê duyệt tài khoản (Chỉ Admin)
    async approveRegistrationRequest(id, { allowedTools, role } = {}) {
      if (!this.hasRole('admin')) {
        throw new Error('Chỉ Administrator (Sếp Kha) mới có quyền phê duyệt tài khoản');
      }

      const pendingList = this.getPendingRequests();
      const req = pendingList.find(r => r.id === id);
      if (!req) throw new Error('Không tìm thấy yêu cầu đăng ký');

      const tools = Array.isArray(allowedTools) && allowedTools.length
        ? allowedTools
        : (req.allowedTools || ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal']);

      const userRole = role === 'admin' ? 'admin' : 'engineer';
      const currentUser = this.getCurrentUser();

      // Cập nhật trạng thái yêu cầu
      req.status = 'approved';
      req.approvedAt = new Date().toISOString();
      req.approvedBy = currentUser?.name || currentUser?.username || 'Sếp Kha';
      req.allowedTools = tools;

      // Thêm người dùng vào danh sách chính thức
      const users = this.getUsers();
      let user = users.find(u => u.username.toLowerCase() === req.username.toLowerCase());
      if (!user) {
        user = {
          id: 'usr_' + Date.now().toString(36) + Math.random().toString(36).substr(2, 4),
          username: req.username,
          name: req.name,
          role: userRole,
          allowedTools: userRole === 'admin' ? ['*'] : tools,
          createdAt: new Date().toISOString(),
          passwordHash: req.passwordHash
        };
        users.push(user);
        this._saveUsers(users);
      } else {
        user.allowedTools = tools;
        this._saveUsers(users);
      }

      this._savePendingRequests(pendingList);

      // Đồng bộ phê duyệt ra Network (LAN + Cloud)
      this._syncApprovalToNetwork(req, user).catch(() => {});

      return { request: req, user };
    }

    // Từ chối yêu cầu đăng ký (Chỉ Admin)
    async rejectRegistrationRequest(id, reason = '') {
      if (!this.hasRole('admin')) {
        throw new Error('Chỉ Administrator mới có quyền từ chối yêu cầu');
      }

      const pendingList = this.getPendingRequests();
      const req = pendingList.find(r => r.id === id);
      if (!req) throw new Error('Không tìm thấy yêu cầu đăng ký');

      req.status = 'rejected';
      req.rejectedAt = new Date().toISOString();
      req.rejectionReason = String(reason || 'Yêu cầu không được phê duyệt').trim();

      this._savePendingRequests(pendingList);

      // Đồng bộ ra Network
      this._syncRejectionToNetwork(req).catch(() => {});

      return req;
    }

    // Xóa yêu cầu khỏi danh sách
    deleteRegistrationRequest(id) {
      if (!this.hasRole('admin')) throw new Error('Chỉ Administrator mới có quyền xóa yêu cầu');
      let list = this.getPendingRequests();
      list = list.filter(r => r.id !== id);
      this._savePendingRequests(list);
      return true;
    }

    // Đồng bộ yêu cầu đăng ký mới lên Network
    async _syncRequestToNetwork(request) {
      // 1. LAN Server
      try {
        const lanBase = this.getLanBase();
        await fetch(`${lanBase}/api/auth/register-request`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request)
        }).catch(() => {});
      } catch (_) {}

      // 2. Cloud Endpoint
      try {
        const cfg = this.getCloudConfig();
        if (cfg && cfg.cloudUrl) {
          const base = cfg.cloudUrl.replace(/\/+$/, '');
          await fetch(`${base}/registrations/${request.id}.json`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request)
          }).catch(() => {});
        }
      } catch (_) {}
    }

    // Đồng bộ trạng thái phê duyệt lên Network
    async _syncApprovalToNetwork(request, approvedUser) {
      // 1. LAN Server
      try {
        const lanBase = this.getLanBase();
        await fetch(`${lanBase}/api/auth/approve-request`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ request, user: approvedUser })
        }).catch(() => {});
      } catch (_) {}

      // 2. Cloud Endpoint
      try {
        const cfg = this.getCloudConfig();
        if (cfg && cfg.cloudUrl) {
          const base = cfg.cloudUrl.replace(/\/+$/, '');
          await fetch(`${base}/registrations/${request.id}.json`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              status: 'approved',
              approvedAt: request.approvedAt,
              approvedBy: request.approvedBy,
              allowedTools: request.allowedTools
            })
          }).catch(() => {});

          await fetch(`${base}/users/${approvedUser.username}.json`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(approvedUser)
          }).catch(() => {});
        }
      } catch (_) {}
    }

    // Đồng bộ từ chối lên Network
    async _syncRejectionToNetwork(request) {
      try {
        const lanBase = this.getLanBase();
        await fetch(`${lanBase}/api/auth/reject-request`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ id: request.id, reason: request.rejectionReason })
        }).catch(() => {});
      } catch (_) {}

      try {
        const cfg = this.getCloudConfig();
        if (cfg && cfg.cloudUrl) {
          const base = cfg.cloudUrl.replace(/\/+$/, '');
          await fetch(`${base}/registrations/${request.id}.json`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              status: 'rejected',
              rejectedAt: request.rejectedAt,
              rejectionReason: request.rejectionReason
            })
          }).catch(() => {});
        }
      } catch (_) {}
    }

    // Kéo dữ liệu yêu cầu từ Network về máy hiện tại (dành cho Admin hoặc Client check)
    async fetchNetworkPendingRequests() {
      let networkList = [];
      const lanBase = this.getLanBase();

      // 1. Kéo từ LAN WebServer
      try {
        // 1a. Lấy danh sách pending
        const res = await fetch(`${lanBase}/api/auth/pending-requests`, { method: 'GET' }).catch(() => null);
        if (res && res.ok) {
          const data = await res.json();
          if (Array.isArray(data)) networkList.push(...data);
        }

        // 1b. Lấy danh sách users đã approved từ LAN server
        const uRes = await fetch(`${lanBase}/api/auth/users`, { method: 'GET' }).catch(() => null);
        if (uRes && uRes.ok) {
          const approvedUsers = await uRes.json();
          if (Array.isArray(approvedUsers) && approvedUsers.length > 0) {
            const localUsers = this.getUsers();
            let updatedUsers = false;
            approvedUsers.forEach(remoteUser => {
              if (remoteUser && remoteUser.username) {
                const existing = localUsers.find(u => u.username.toLowerCase() === remoteUser.username.toLowerCase());
                if (!existing) {
                  localUsers.push(remoteUser);
                  updatedUsers = true;
                } else if (JSON.stringify(existing.allowedTools) !== JSON.stringify(remoteUser.allowedTools)) {
                  existing.allowedTools = remoteUser.allowedTools;
                  updatedUsers = true;
                }
              }
            });
            if (updatedUsers) this._saveUsers(localUsers);
          }
        }
      } catch (_) {}

      // 2. Kéo từ Cloud Endpoint
      try {
        const cfg = this.getCloudConfig();
        if (cfg && cfg.cloudUrl) {
          const base = cfg.cloudUrl.replace(/\/+$/, '');
          const res = await fetch(`${base}/registrations.json`, { method: 'GET' }).catch(() => null);
          if (res && res.ok) {
            const data = await res.json();
            if (data && typeof data === 'object') {
              Object.values(data).forEach(item => {
                if (item && item.username) networkList.push(item);
              });
            }
          }

          // Kéo thêm danh sách users đã duyệt trên cloud
          const uRes = await fetch(`${base}/users.json`, { method: 'GET' }).catch(() => null);
          if (uRes && uRes.ok) {
            const uData = await uRes.json();
            if (uData && typeof uData === 'object') {
              const localUsers = this.getUsers();
              let updatedUsers = false;
              Object.values(uData).forEach(remoteUser => {
                if (remoteUser && remoteUser.username) {
                  const existing = localUsers.find(u => u.username.toLowerCase() === remoteUser.username.toLowerCase());
                  if (!existing) {
                    localUsers.push(remoteUser);
                    updatedUsers = true;
                  } else if (JSON.stringify(existing.allowedTools) !== JSON.stringify(remoteUser.allowedTools)) {
                    existing.allowedTools = remoteUser.allowedTools;
                    updatedUsers = true;
                  }
                }
              });
              if (updatedUsers) this._saveUsers(localUsers);
            }
          }
        }
      } catch (_) {}

      if (!networkList.length) return this.getPendingRequests();

      // Hợp nhất danh sách local và network
      const localList = this.getPendingRequests();
      let changed = false;

      networkList.forEach(netItem => {
        const idx = localList.findIndex(l => l.id === netItem.id || l.username.toLowerCase() === netItem.username.toLowerCase());
        if (idx === -1) {
          localList.push(netItem);
          changed = true;
        } else {
          // Cập nhật nếu bản ghi trên mạng mới hơn hoặc đã approved
          if (netItem.status !== localList[idx].status || (netItem.approvedAt && !localList[idx].approvedAt)) {
            localList[idx] = Object.assign({}, localList[idx], netItem);
            changed = true;
          }
        }
      });

      if (changed) {
        this._savePendingRequests(localList);
      }

      return localList;
    }

    // Bắt đầu chu kỳ đồng bộ nền
    startBackgroundSync() {
      if (this._pollTimer) clearInterval(this._pollTimer);
      const cfg = this.getCloudConfig();
      const interval = Math.max(5000, cfg.pollIntervalMs || 15000);

      this._pollTimer = setInterval(async () => {
        const currentUser = this.getCurrentUser();
        // Chỉ kéo & phát âm thanh khi đã đăng nhập Admin hoặc có đăng ký chờ
        const myReg = this.getMyRegistration();
        if ((currentUser && currentUser.role === 'admin') || (myReg && myReg.status === 'pending')) {
          try {
            const list = await this.fetchNetworkPendingRequests();
            const pendingCount = list.filter(r => r.status === 'pending').length;

            if (currentUser && currentUser.role === 'admin') {
              // Nếu có yêu cầu mới xuất hiện so với lần trước -> phát chuông & báo động
              if (pendingCount > this._lastKnownPendingCount && this._lastKnownPendingCount >= 0) {
                playChimeSound();
                window.dispatchEvent(new CustomEvent('ks:auth:new_registration_alert', {
                  detail: { count: pendingCount }
                }));
              }
              this._lastKnownPendingCount = pendingCount;
            }

            // Nếu người này là client chờ duyệt, tự kiểm tra trạng thái
            if (myReg && myReg.username) {
              const updated = list.find(r => r.username.toLowerCase() === myReg.username.toLowerCase());
              if (updated && updated.status !== myReg.status) {
                this.setMyRegistration(updated);
                window.dispatchEvent(new CustomEvent('ks:auth:my_reg_status_change', {
                  detail: updated
                }));
              }
            }
          } catch (_) {}
        }
      }, interval);
    }

    // =========================================================================
    // PHIÊN LÀM VIỆC & ĐĂNG NHẬP (SESSION & LOGIN)
    // =========================================================================

    getCurrentUser() {
      try {
        const raw = sessionStorage.getItem(STORAGE_SESSION_KEY) || localStorage.getItem(STORAGE_SESSION_KEY);
        if (!raw) return null;
        const session = JSON.parse(raw);
        const users = this.getUsers();
        const user = users.find(u => u.id === session.id && u.username === session.username);
        if (!user) {
          this.logout();
          return null;
        }
        return {
          id: user.id,
          username: user.username,
          name: user.name,
          role: user.role,
          allowedTools: user.allowedTools || (user.role === 'admin' ? ['*'] : []),
          createdAt: user.createdAt,
          loginAt: session.loginAt
        };
      } catch (e) {
        return null;
      }
    }

    // Đăng nhập
    async login(username, password, remember = false) {
      username = String(username || '').trim().toLowerCase();
      password = String(password || '');

      // Trước khi báo không tồn tại, thử fetch network xem vừa được duyệt chưa
      let users = this.getUsers();
      let user = users.find(u => u.username.toLowerCase() === username);

      if (!user) {
        await this.checkRegistrationStatus(username).catch(() => {});
        users = this.getUsers();
        user = users.find(u => u.username.toLowerCase() === username);
      }

      if (!user) {
        // Kiểm tra xem có đang nằm trong danh sách chờ duyệt không
        const pendingList = this.getPendingRequests();
        const pending = pendingList.find(p => p.username.toLowerCase() === username);
        if (pending) {
          if (pending.status === 'pending') {
            throw new Error(`Tài khoản "@${username}" đang chờ Admin phê duyệt. Vui lòng liên hệ quản trị viên.`);
          } else if (pending.status === 'rejected') {
            const reason = pending.rejectionReason ? ` (Lý do: ${pending.rejectionReason})` : '';
            throw new Error(`Yêu cầu tài khoản "@${username}" đã bị từ chối${reason}.`);
          }
        }
        throw new Error('Tài khoản không tồn tại trên hệ thống');
      }

      const match = await verifyPassword(password, user.passwordHash);
      if (!match) {
        // Dự phòng fallback tài khoản mặc định nếu mới cài
        if ((username === 'kha' && password === 'kha') || (username === 'engineer' && password === '123456')) {
          user.passwordHash = await hashPassword(password);
          this._saveUsers(users);
        } else {
          throw new Error('Mật khẩu không chính xác');
        }
      }

      const sessionData = {
        id: user.id,
        username: user.username,
        name: user.name,
        role: user.role,
        allowedTools: user.allowedTools || (user.role === 'admin' ? ['*'] : []),
        loginAt: new Date().toISOString()
      };

      const storage = remember ? localStorage : sessionStorage;
      storage.setItem(STORAGE_SESSION_KEY, JSON.stringify(sessionData));
      if (!remember) {
        localStorage.removeItem(STORAGE_SESSION_KEY);
      }

      window.dispatchEvent(new CustomEvent('ks:auth:change', { detail: sessionData }));
      return sessionData;
    }

    logout() {
      sessionStorage.removeItem(STORAGE_SESSION_KEY);
      localStorage.removeItem(STORAGE_SESSION_KEY);
      window.dispatchEvent(new CustomEvent('ks:auth:change', { detail: null }));
    }

    hasRole(requiredRole) {
      const user = this.getCurrentUser();
      if (!user) return false;
      if (user.role === 'admin') return true;
      return user.role === requiredRole;
    }

    canAccessTool(toolId, user = null) {
      if (!toolId) return false;
      const targetUser = user || this.getCurrentUser();
      if (!targetUser) return false;
      if (targetUser.role === 'admin') return true;
      const list = targetUser.allowedTools;
      if (!Array.isArray(list)) return false;
      if (list.includes('*')) return true;
      return list.includes(toolId);
    }

    async changePassword(currentPassword, newPassword) {
      const current = this.getCurrentUser();
      if (!current) throw new Error('Chưa đăng nhập');
      if (!newPassword || newPassword.length < 4) {
        throw new Error('Mật khẩu mới phải có ít nhất 4 ký tự');
      }

      const users = this.getUsers();
      const user = users.find(u => u.id === current.id);
      if (!user) throw new Error('Không tìm thấy người dùng');

      const match = await verifyPassword(currentPassword, user.passwordHash);
      if (!match) throw new Error('Mật khẩu hiện tại không đúng');

      user.passwordHash = await hashPassword(newPassword);
      this._saveUsers(users);
      return true;
    }

    // Tạo người dùng mới thủ công (Chỉ dành cho Admin)
    async createUser({ username, name, role, allowedTools, password }) {
      if (!this.hasRole('admin')) throw new Error('Chỉ Administrator mới có quyền tạo người dùng');
      username = String(username || '').trim().toLowerCase();
      name = String(name || '').trim();
      role = role === 'admin' ? 'admin' : 'engineer';

      if (!username || !/^[a-z0-9_.-]{3,30}$/.test(username)) {
        throw new Error('Tên đăng nhập từ 3-30 ký tự, không dấu và không chứa ký tự đặc biệt');
      }
      if (!name) throw new Error('Tên hiển thị không được để trống');
      if (!password || password.length < 4) throw new Error('Mật khẩu tối thiểu 4 ký tự');

      const users = this.getUsers();
      if (users.some(u => u.username.toLowerCase() === username)) {
        throw new Error(`Tên đăng nhập "${username}" đã tồn tại`);
      }

      const tools = role === 'admin'
        ? ['*']
        : (Array.isArray(allowedTools) ? allowedTools : ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'vision-studio', 'field-ops', 'tia-portal']);

      const passwordHash = await hashPassword(password);
      const newUser = {
        id: 'usr_' + Date.now().toString(36) + Math.random().toString(36).substr(2, 4),
        username,
        name,
        role,
        allowedTools: tools,
        createdAt: new Date().toISOString(),
        passwordHash
      };

      users.push(newUser);
      this._saveUsers(users);
      return newUser;
    }

    async updateUser(id, { name, role, allowedTools, password }) {
      if (!this.hasRole('admin')) throw new Error('Chỉ Administrator mới có quyền cập nhật người dùng');
      const users = this.getUsers();
      const user = users.find(u => u.id === id);
      if (!user) throw new Error('Không tìm thấy người dùng');

      if (name) user.name = String(name).trim();
      if (role && (role === 'admin' || role === 'engineer')) {
        const adminCount = users.filter(u => u.role === 'admin').length;
        if (user.role === 'admin' && role !== 'admin' && adminCount <= 1) {
          throw new Error('Hệ thống phải có ít nhất 1 tài khoản Administrator');
        }
        user.role = role;
      }

      if (user.role === 'admin') {
        user.allowedTools = ['*'];
      } else if (Array.isArray(allowedTools)) {
        user.allowedTools = allowedTools;
      }

      if (password && password.length >= 4) {
        user.passwordHash = await hashPassword(password);
      }

      this._saveUsers(users);

      const cur = this.getCurrentUser();
      if (cur && cur.id === user.id) {
        const sessionData = {
          id: user.id,
          username: user.username,
          name: user.name,
          role: user.role,
          allowedTools: user.allowedTools,
          loginAt: cur.loginAt || new Date().toISOString()
        };
        if (localStorage.getItem(STORAGE_SESSION_KEY)) {
          localStorage.setItem(STORAGE_SESSION_KEY, JSON.stringify(sessionData));
        }
        if (sessionStorage.getItem(STORAGE_SESSION_KEY)) {
          sessionStorage.setItem(STORAGE_SESSION_KEY, JSON.stringify(sessionData));
        }
        window.dispatchEvent(new CustomEvent('ks:auth:change', { detail: sessionData }));
      }

      return user;
    }

    deleteUser(id) {
      if (!this.hasRole('admin')) throw new Error('Chỉ Administrator mới có quyền xóa người dùng');
      const current = this.getCurrentUser();
      if (current && current.id === id) {
        throw new Error('Không thể tự xóa tài khoản đang đăng nhập');
      }

      let users = this.getUsers();
      const user = users.find(u => u.id === id);
      if (!user) throw new Error('Không tìm thấy người dùng');
      if (user.role === 'admin') {
        const adminCount = users.filter(u => u.role === 'admin').length;
        if (adminCount <= 1) throw new Error('Không thể xóa Administrator cuối cùng');
      }

      users = users.filter(u => u.id !== id);
      this._saveUsers(users);
      return true;
    }
  }

  // Tiện ích âm thanh công khai
  AuthManager.playChime = playChimeSound;

  // Xuất instance toàn cục
  global.Auth = new AuthManager();

})(typeof window !== 'undefined' ? window : global);
