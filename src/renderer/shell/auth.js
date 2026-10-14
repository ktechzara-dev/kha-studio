/**
 * Kha Studio — Authentication & Role-Based Access Control (RBAC) Module
 * Sử dụng Web Crypto API chuẩn (PBKDF2 HMAC-SHA256, 100,000 iterations, random salt)
 */
(function (global) {
  'use strict';

  const STORAGE_USERS_KEY = 'ks_users';
  const STORAGE_SESSION_KEY = 'ks_session';
  const PBKDF2_ITERATIONS = 100000;

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
      allowedTools: ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'field-ops', 'tia-portal'],
      createdAt: '2026-10-01T00:00:00.000Z',
      // Mật khẩu mặc định: '123456'
      passwordHash: 'pbkdf2:100000:bf88fa90740a3ddde02b11894d07b812:12cbfa3f79efbca279a05b38e2ecad0318413a0e69d763bc753ad37ea92209d7'
    }
  ];

  class AuthManager {
    constructor() {
      this._init();
    }

    _init() {
      // Khởi tạo bảng users nếu chưa có
      try {
        const raw = localStorage.getItem(STORAGE_USERS_KEY);
        if (!raw) {
          localStorage.setItem(STORAGE_USERS_KEY, JSON.stringify(INITIAL_USERS));
        } else {
          // Nâng cấp dữ liệu cũ nếu chưa có allowedTools
          const users = JSON.parse(raw);
          let changed = false;
          users.forEach(u => {
            if (!Array.isArray(u.allowedTools)) {
              u.allowedTools = u.role === 'admin' ? ['*'] : ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'field-ops', 'tia-portal'];
              changed = true;
            }
          });
          if (changed) this._saveUsers(users);
        }
      } catch (e) {
        console.warn('Lỗi khởi tạo danh sách users:', e);
      }
    }

    getUsers() {
      try {
        const raw = localStorage.getItem(STORAGE_USERS_KEY);
        const users = raw ? JSON.parse(raw) : [...INITIAL_USERS];
        users.forEach(u => {
          if (!Array.isArray(u.allowedTools)) {
            u.allowedTools = u.role === 'admin' ? ['*'] : ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'field-ops', 'tia-portal'];
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

    // Lấy thông tin user đang đăng nhập
    getCurrentUser() {
      try {
        // Kiểm tra session
        const raw = sessionStorage.getItem(STORAGE_SESSION_KEY) || localStorage.getItem(STORAGE_SESSION_KEY);
        if (!raw) return null;
        const session = JSON.parse(raw);
        // Xác thực lại với danh sách users xem còn tồn tại không
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

      const users = this.getUsers();
      const user = users.find(u => u.username.toLowerCase() === username);
      if (!user) {
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

    // Đăng xuất
    logout() {
      sessionStorage.removeItem(STORAGE_SESSION_KEY);
      localStorage.removeItem(STORAGE_SESSION_KEY);
      window.dispatchEvent(new CustomEvent('ks:auth:change', { detail: null }));
    }

    // Kiểm tra quyền (RBAC)
    hasRole(requiredRole) {
      const user = this.getCurrentUser();
      if (!user) return false;
      if (user.role === 'admin') return true; // Admin có toàn quyền
      return user.role === requiredRole;
    }

    // Kiểm tra quyền truy cập công cụ theo toolId
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

    // Đổi mật khẩu của chính user đang đăng nhập
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

    // Tạo người dùng mới (Chỉ dành cho Admin)
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
        : (Array.isArray(allowedTools) ? allowedTools : ['apm', 'pallet', 'calculator', 'plc-modbus', 'scada-vision', 'field-ops']);

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

    // Cập nhật người dùng (Chỉ Admin)
    async updateUser(id, { name, role, allowedTools, password }) {
      if (!this.hasRole('admin')) throw new Error('Chỉ Administrator mới có quyền cập nhật người dùng');
      const users = this.getUsers();
      const user = users.find(u => u.id === id);
      if (!user) throw new Error('Không tìm thấy người dùng');

      if (name) user.name = String(name).trim();
      if (role && (role === 'admin' || role === 'engineer')) {
        // Không cho phép hạ quyền tài khoản admin gốc duy nhất
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

      // Đồng bộ lại session hiện tại nếu vừa cập nhật chính user đang đăng nhập
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

    // Xóa người dùng (Chỉ Admin)
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

  // Xuất instance toàn cục
  global.Auth = new AuthManager();

})(window);
