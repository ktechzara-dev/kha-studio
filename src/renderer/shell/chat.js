/**
 * Kha Studio — Realtime Team Chat & Online Presence Module
 * Hỗ trợ trò chuyện kỹ thuật nội bộ & theo dõi hiện diện trực tuyến (Online Presence)
 * Kết nối Realtime 2 chiều qua Firebase Cloud & LAN WebServer.
 */
(function (global) {
  'use strict';

  const DEFAULT_FIREBASE_URL = 'https://k-core-studio-default-rtdb.asia-southeast1.firebasedatabase.app';
  const HEARTBEAT_INTERVAL_MS = 25000; // 25s gửi heartbeat 1 lần
  const ONLINE_THRESHOLD_MS = 75000;    // 75s không có tín hiệu coi là offline
  const CHAT_POLL_ACTIVE_MS = 3000;     // 3s poll tin nhắn khi mở khung chat
  const CHAT_POLL_IDLE_MS = 8000;       // 8s poll tin nhắn khi đóng khung chat

  class ChatManager {
    constructor() {
      this.messages = [];
      this.presenceMap = {};
      this.unreadCount = 0;
      this.isOpen = false;
      this.currentTab = 'chat'; // 'chat' | 'presence'
      this._heartbeatTimer = null;
      this._pollTimer = null;
      this._lastSeenMsgKey = null;
      this._isInit = false;
    }

    getFirebaseBase() {
      try {
        if (window.Auth && typeof window.Auth.getCloudConfig === 'function') {
          const cfg = window.Auth.getCloudConfig();
          if (cfg && cfg.cloudUrl) return cfg.cloudUrl.replace(/\/+$/, '');
        }
      } catch (_) {}
      return DEFAULT_FIREBASE_URL;
    }

    getCurrentUser() {
      try {
        if (window.Auth && typeof window.Auth.getCurrentUser === 'function') {
          return window.Auth.getCurrentUser();
        }
      } catch (_) {}
      return { username: 'kha', name: 'Sếp Kha', role: 'admin' };
    }

    init() {
      if (this._isInit) return;
      this._isInit = true;

      // 1. Gửi heartbeat ngay lập tức và định kỳ
      this.sendHeartbeat();
      this._heartbeatTimer = setInterval(() => this.sendHeartbeat(), HEARTBEAT_INTERVAL_MS);

      // 2. Kéo danh sách tin nhắn và hiện diện
      this.fetchPresence();
      this.fetchMessages(true);

      // 3. Chu kỳ làm mới tin nhắn & presence
      this._startPolling();

      // Khi người dùng tắt hoặc đóng trang -> đánh dấu offline
      window.addEventListener('beforeunload', () => {
        this.markOffline();
      });

      // Lắng nghe thay đổi tài khoản
      window.addEventListener('ks:auth:change', () => {
        this.sendHeartbeat();
        this.fetchPresence();
      });
    }

    _startPolling() {
      if (this._pollTimer) clearInterval(this._pollTimer);
      const interval = this.isOpen ? CHAT_POLL_ACTIVE_MS : CHAT_POLL_IDLE_MS;
      this._pollTimer = setInterval(async () => {
        await Promise.all([this.fetchMessages(false), this.fetchPresence()]);
      }, interval);
    }

    // =========================================================================
    // 1. ONLINE PRESENCE (THEO DÕI HIỆN DIỆN TRỰC TUYẾN)
    // =========================================================================

    async sendHeartbeat() {
      const user = this.getCurrentUser();
      if (!user || !user.username) return;

      const safeKey = user.username.replace(/\./g, '_');
      const base = this.getFirebaseBase();

      const payload = {
        username: user.username,
        name: user.name || user.username,
        role: user.role || 'engineer',
        platform: 'desktop',
        lastSeen: new Date().toISOString(),
        isOnline: true
      };

      try {
        await fetch(`${base}/presence/${safeKey}.json`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json; charset=utf-8' },
          body: JSON.stringify(payload)
        });
      } catch (_) {}
    }

    async markOffline() {
      const user = this.getCurrentUser();
      if (!user || !user.username) return;
      const safeKey = user.username.replace(/\./g, '_');
      const base = this.getFirebaseBase();
      try {
        const payload = JSON.stringify({ isOnline: false, lastSeen: new Date().toISOString() });
        navigator.sendBeacon(`${base}/presence/${safeKey}.json`, payload);
      } catch (_) {}
    }

    async fetchPresence() {
      const base = this.getFirebaseBase();
      try {
        const res = await fetch(`${base}/presence.json`).catch(() => null);
        if (res && res.ok) {
          const data = await res.json();
          if (data && typeof data === 'object') {
            this.presenceMap = data;
            this.updatePresenceUI();
          }
        }
      } catch (_) {}
    }

    getOnlineUsers() {
      const now = Date.now();
      const list = [];
      for (const [key, p] of Object.entries(this.presenceMap)) {
        if (!p || !p.username) continue;
        const last = p.lastSeen ? new Date(p.lastSeen).getTime() : 0;
        const isRecentlyActive = (now - last) < ONLINE_THRESHOLD_MS;
        list.push({
          ...p,
          isOnline: isRecentlyActive && (p.isOnline !== false),
          lastSeenDate: p.lastSeen ? new Date(p.lastSeen) : null
        });
      }
      // Ưu tiên hiển thị: Admin trước, người online trước, sau đó theo tên
      list.sort((a, b) => {
        if (a.isOnline !== b.isOnline) return b.isOnline ? 1 : -1;
        if (a.role === 'admin' && b.role !== 'admin') return -1;
        if (b.role === 'admin' && a.role !== 'admin') return 1;
        return (a.name || a.username).localeCompare(b.name || b.username);
      });
      return list;
    }

    updatePresenceUI() {
      const users = this.getOnlineUsers();
      const onlineCount = users.filter(u => u.isOnline).length;

      // Cập nhật chấm xanh trên Header và Sidebar
      const headerDot = document.getElementById('online-indicator-dot');
      const sbDot = document.getElementById('sb-online-dot');
      const onlineCountLbl = document.getElementById('kchat-online-count-lbl');

      if (headerDot) headerDot.style.display = onlineCount > 0 ? 'block' : 'none';
      if (sbDot) sbDot.title = `${onlineCount} người đang trực tuyến`;
      if (onlineCountLbl) onlineCountLbl.textContent = `${onlineCount} Online`;

      // Render danh sách ở Tab Presence nếu đang mở
      this.renderPresenceList(users);
    }

    renderPresenceList(users) {
      const container = document.getElementById('kchat-presence-list');
      if (!container) return;

      if (!users.length) {
        container.innerHTML = `
          <div style="text-align:center; padding:30px 16px; color:var(--text-3); font-size:12.5px;">
            Chưa có thông tin hiện diện thành viên.
          </div>
        `;
        return;
      }

      container.innerHTML = users.map(u => {
        const isAdm = u.role === 'admin' || u.username === 'kha';
        const roleText = isAdm ? '👑 Quản trị viên' : '👷 Kỹ sư';
        const platformIcon = u.platform === 'mobile' ? '📱 Mobile' : '💻 Máy tính';
        const statusBadge = u.isOnline
          ? `<span class="presence-badge online"><span class="presence-dot-pulse"></span> Đang Online</span>`
          : `<span class="presence-badge offline">⚪ ${this.formatRelativeTime(u.lastSeenDate)}</span>`;

        const initial = (u.name || u.username || 'U').charAt(0).toUpperCase();

        return `
          <div class="presence-card ${u.isOnline ? 'is-online' : ''}">
            <div class="presence-avatar ${isAdm ? 'adm' : ''}">
              <span>${initial}</span>
              <span class="presence-dot ${u.isOnline ? 'on' : 'off'}"></span>
            </div>
            <div class="presence-info">
              <div class="presence-name-row">
                <span class="presence-name">${this.escapeHtml(u.name || u.username)}</span>
                ${statusBadge}
              </div>
              <div class="presence-meta">
                <span>@${this.escapeHtml(u.username)}</span> &bull; 
                <span style="color:var(--accent); font-weight:600;">${roleText}</span> &bull; 
                <span>${platformIcon}</span>
              </div>
            </div>
            <button class="presence-action-btn" onclick="window.Chat.tagUser('${this.escapeHtml(u.name || u.username)}')" title="Nhắn tin cho ${this.escapeHtml(u.name || u.username)}">
              💬
            </button>
          </div>
        `;
      }).join('');
    }

    // =========================================================================
    // 2. REALTIME TEAM MESSAGES (TRÒ CHUYỆN NỘI BỘ)
    // =========================================================================

    async fetchMessages(isFirstLoad = false) {
      const base = this.getFirebaseBase();
      try {
        const res = await fetch(`${base}/team_messages.json?limitToLast=80`).catch(() => null);
        if (res && res.ok) {
          const data = await res.json();
          if (data && typeof data === 'object') {
            const list = Object.entries(data).map(([k, v]) => ({ key: k, ...v }));
            // Sắp xếp theo thời gian tăng dần
            list.sort((a, b) => new Date(a.createdAt || 0) - new Date(b.createdAt || 0));

            // Kiểm tra tin nhắn mới để phát chuông & báo unread
            if (!isFirstLoad && list.length > 0) {
              const lastMsg = list[list.length - 1];
              const currentUser = this.getCurrentUser();
              if (lastMsg.key !== this._lastSeenMsgKey && lastMsg.username !== currentUser.username) {
                // Có tin nhắn mới từ người khác
                this._lastSeenMsgKey = lastMsg.key;
                if (!this.isOpen) {
                  this.unreadCount++;
                  this.updateUnreadBadge();
                  this.playChime();
                  this.showChatToast(lastMsg);
                }
              }
            }

            if (list.length > 0) {
              this._lastSeenMsgKey = list[list.length - 1].key;
            }

            this.messages = list;
            this.renderMessages();
          }
        }
      } catch (_) {}
    }

    async sendMessage(text) {
      text = String(text || '').trim();
      if (!text) return;

      const user = this.getCurrentUser();
      const base = this.getFirebaseBase();

      const timeStr = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
      const msgObj = {
        id: 'msg_' + Date.now().toString(36) + Math.random().toString(36).substr(2, 4),
        username: user.username,
        senderName: user.name || user.username,
        role: user.role || 'engineer',
        platform: 'desktop',
        text: text,
        timestamp: timeStr,
        createdAt: new Date().toISOString()
      };

      // Đẩy lạc quan (Optimistic UI) vào danh sách hiển thị ngay lập tức
      this.messages.push(msgObj);
      this.renderMessages();
      this.scrollToBottom();

      try {
        await fetch(`${base}/team_messages.json`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json; charset=utf-8' },
          body: JSON.stringify(msgObj)
        });
      } catch (e) {
        console.warn('Lỗi gửi tin nhắn:', e);
      }
    }

    renderMessages() {
      const container = document.getElementById('kchat-messages-container');
      if (!container) return;

      if (!this.messages.length) {
        container.innerHTML = `
          <div class="kchat-empty">
            <span style="font-size:36px;">💬</span>
            <div style="font-weight:700; margin-top:8px;">Kênh Trò Chuyện Kỹ Thuật Nội Bộ</div>
            <p style="color:var(--text-3); font-size:12px; margin-top:4px;">
              Trao đổi tiến độ, thông báo site và kỹ thuật 2 chiều giữa Văn phòng & Công trường!
            </p>
          </div>
        `;
        return;
      }

      const currentUser = this.getCurrentUser();
      const curUname = (currentUser.username || '').toLowerCase();

      let lastDateStr = '';
      let html = '';

      this.messages.forEach(m => {
        const isMe = (m.username || '').toLowerCase() === curUname;
        const isAdm = m.role === 'admin' || (m.username || '').toLowerCase() === 'kha';
        const roleBadge = isAdm ? '👑 Admin' : '👷 Kỹ sư';
        const platformBadge = m.platform === 'mobile' ? '📱 Mobile' : '💻 PC';

        const msgDate = m.createdAt ? new Date(m.createdAt).toLocaleDateString('vi-VN') : '';
        if (msgDate && msgDate !== lastDateStr) {
          lastDateStr = msgDate;
          html += `<div class="kchat-date-divider"><span>${msgDate}</span></div>`;
        }

        html += `
          <div class="kchat-msg-row ${isMe ? 'is-me' : 'is-other'}">
            ${!isMe ? `
              <div class="kchat-msg-avatar ${isAdm ? 'adm' : ''}">
                ${(m.senderName || m.username || 'U').charAt(0).toUpperCase()}
              </div>
            ` : ''}
            <div class="kchat-bubble-wrap">
              ${!isMe ? `
                <div class="kchat-sender-info">
                  <strong class="kchat-sender-name">${this.escapeHtml(m.senderName || m.username)}</strong>
                  <span class="kchat-role-tag ${isAdm ? 'adm' : ''}">${roleBadge}</span>
                  <span class="kchat-plat-tag">${platformBadge}</span>
                </div>
              ` : ''}
              <div class="kchat-bubble ${isMe ? 'me' : 'other'}">
                <div class="kchat-text">${this.formatMessageText(m.text)}</div>
                <div class="kchat-time">${this.escapeHtml(m.timestamp || '')}</div>
              </div>
            </div>
          </div>
        `;
      });

      container.innerHTML = html;
      this.scrollToBottom();
    }

    formatMessageText(text) {
      if (!text) return '';
      let escaped = this.escapeHtml(text);
      // Hỗ trợ link URL
      escaped = escaped.replace(/(https?:\/\/[^\s]+)/g, '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>');
      // Highlight mention @tên
      escaped = escaped.replace(/(@[a-zA-Z0-9_\u00C0-\u1EF9\.\s]+)/g, '<span class="kchat-mention">$1</span>');
      // Xuống dòng
      return escaped.replace(/\n/g, '<br>');
    }

    scrollToBottom() {
      const container = document.getElementById('kchat-messages-container');
      if (container) {
        container.scrollTop = container.scrollHeight;
      }
    }

    // =========================================================================
    // 3. ĐIỀU KHIỂN GIAO DIỆN & TIỆN ÍCH
    // =========================================================================

    toggle() {
      if (this.isOpen) {
        this.close();
      } else {
        this.open();
      }
    }

    open(tab = 'chat') {
      const drawer = document.getElementById('kchat-drawer');
      if (!drawer) return;
      drawer.classList.add('open');
      this.isOpen = true;
      this.unreadCount = 0;
      this.updateUnreadBadge();
      this.setTab(tab);
      this._startPolling();
      this.fetchMessages();
      this.fetchPresence();
      setTimeout(() => {
        const input = document.getElementById('kchat-input');
        if (input) input.focus();
        this.scrollToBottom();
      }, 150);
    }

    close() {
      const drawer = document.getElementById('kchat-drawer');
      if (drawer) drawer.classList.remove('open');
      this.isOpen = false;
      this._startPolling();
    }

    setTab(tabName) {
      this.currentTab = tabName;
      const tabChatBtn = document.getElementById('kchat-tab-btn-chat');
      const tabPresBtn = document.getElementById('kchat-tab-btn-presence');
      const viewChat = document.getElementById('kchat-view-chat');
      const viewPres = document.getElementById('kchat-view-presence');

      if (tabChatBtn && tabPresBtn && viewChat && viewPres) {
        if (tabName === 'chat') {
          tabChatBtn.classList.add('active');
          tabPresBtn.classList.remove('active');
          viewChat.style.display = 'flex';
          viewPres.style.display = 'none';
          this.scrollToBottom();
        } else {
          tabChatBtn.classList.remove('active');
          tabPresBtn.classList.add('active');
          viewChat.style.display = 'none';
          viewPres.style.display = 'block';
          this.fetchPresence();
        }
      }
    }

    tagUser(name) {
      this.setTab('chat');
      const input = document.getElementById('kchat-input');
      if (input) {
        const val = input.value.trim();
        input.value = val ? `${val} @${name} ` : `@${name} `;
        input.focus();
      }
    }

    sendQuick(text) {
      this.sendMessage(text);
    }

    updateUnreadBadge() {
      const badgeHeader = document.getElementById('chat-badge');
      const badgeSidebar = document.getElementById('sb-chat-badge');
      const badgeFloating = document.getElementById('floating-chat-badge');

      const countStr = this.unreadCount > 9 ? '9+' : String(this.unreadCount);
      const isVisible = this.unreadCount > 0;

      [badgeHeader, badgeSidebar, badgeFloating].forEach(el => {
        if (el) {
          el.textContent = countStr;
          el.style.display = isVisible ? 'inline-block' : 'none';
        }
      });
    }

    showChatToast(msg) {
      try {
        if (typeof window.notify === 'function') {
          window.notify(
            `💬 ${msg.senderName || msg.username}:`,
            msg.text.length > 50 ? msg.text.substring(0, 50) + '…' : msg.text,
            'info'
          );
        }
      } catch (_) {}
    }

    playChime() {
      try {
        if (window.Auth && typeof window.Auth.constructor.playChime === 'function') {
          window.Auth.constructor.playChime();
        }
      } catch (_) {}
    }

    formatRelativeTime(date) {
      if (!date || isNaN(date.getTime())) return 'Chưa rõ';
      const diffSec = Math.floor((Date.now() - date.getTime()) / 1000);
      if (diffSec < 60) return 'Vừa mới đây';
      if (diffSec < 3600) return `${Math.floor(diffSec / 60)} phút trước`;
      if (diffSec < 86400) return `${Math.floor(diffSec / 3600)} giờ trước`;
      return date.toLocaleDateString('vi-VN');
    }

    escapeHtml(str) {
      return String(str || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }
  }

  // Khởi tạo đối tượng toàn cục
  global.Chat = new ChatManager();

  // Tự động khởi động khi trang tải xong
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => global.Chat.init());
  } else {
    global.Chat.init();
  }

})(typeof window !== 'undefined' ? window : global);
