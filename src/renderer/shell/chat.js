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
      this._eventSource = null;
      this._lastSeenMsgKey = null;
      this._isInit = false;
      this._activeAudio = null;
      this._activeAudioBtn = null;
      this._mediaRecorder = null;
      this._audioChunks = [];
      this._isRecording = false;
      this._recTimer = null;
      this._recSeconds = 0;
    }

    fixViText(str) {
      if (!str) return '';
      let res = String(str);
      if (/[\u00C0-\u00FF]/.test(res)) {
        try {
          const decoded = decodeURIComponent(escape(res));
          if (decoded) res = decoded;
        } catch (_) {}
      }
      return res
        .replace(/S[\?\uFFFD]p\s*Kha|Sp\s*Kha|S\?p\s*Kha|Sáº¿p\s*Kha|S\?p Kha/gi, 'S\u1EBFp Kha')
        .replace(/Duy[\?\uFFFD]n1|DuyÃªn 1/gi, 'Duy\u00EAn 1')
        .replace(/Duy[\?\uFFFD]n|DuyÃªn/gi, 'Duy\u00EAn')
        .replace(/Di[\?\uFFFD]u|Diá»‡u/gi, 'Di\u1EC7u');
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
      return { username: 'kha', name: 'Admin', role: 'admin' };
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

      // 3. Kết nối Realtime SSE Stream để nhận tin nhắn tức thì (0 delay)
      this._initRealtimeStream();

      // 4. Chu kỳ làm mới tin nhắn & presence (Polling dự phòng)
      this._startPolling();

      // Khi người dùng tắt hoặc đóng trang -> đánh dấu offline
      window.addEventListener('beforeunload', () => {
        if (this._eventSource) {
          try { this._eventSource.close(); } catch (_) {}
          this._eventSource = null;
        }
        this.markOffline();
      });

      // Lắng nghe thay đổi tài khoản
      window.addEventListener('ks:auth:change', () => {
        this.sendHeartbeat();
        this.fetchPresence();
        this._initRealtimeStream();
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
      const isAdmUser = user.role === 'admin' || user.username === 'kha' || (user.name && user.name.toLowerCase().includes('admin'));

      const payload = {
        username: user.username,
        name: isAdmUser ? 'Admin' : this.fixViText(user.name || user.username),
        role: isAdmUser ? 'admin' : (user.role || 'engineer'),
        avatarUrl: user.avatarUrl || null,
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

      const onlineCount = users.filter(u => u.isOnline).length;
      const totalCount = users.length;

      let html = `
        <div class="kchat-presence-summary">
          <div class="presence-stat-pill online">
            <span class="presence-radar-pulse"></span>
            <strong>${onlineCount}</strong> Đang Trực Tuyến
          </div>
          <div class="presence-stat-pill total">
            👥 <strong>${totalCount}</strong> Thành Viên
          </div>
        </div>
      `;

      html += users.map(u => {
        const isAdm = u.role === 'admin' || (u.username || '').toLowerCase() === 'kha' || (u.name && u.name.toLowerCase().includes('kha'));
        const roleText = isAdm ? '👑 Quản trị viên' : '👷 Kỹ sư';
        const platformIcon = u.platform === 'mobile' ? '📱 Mobile App' : '💻 PC Desktop';
        const statusBadge = u.isOnline
          ? `<span class="presence-badge online"><span class="presence-dot-pulse"></span> Đang Online</span>`
          : `<span class="presence-badge offline">⚪ ${this.formatRelativeTime(u.lastSeenDate)}</span>`;

        const displayName = isAdm ? 'Admin' : this.escapeHtml(this.fixViText(u.name || u.username));
        const initial = displayName.charAt(0).toUpperCase();
        const avatarHtml = u.avatarUrl
          ? `<img src="${u.avatarUrl}" class="presence-avatar-img" alt="${displayName}">`
          : `<span>${initial}</span>`;

        return `
          <div class="presence-card ${u.isOnline ? 'is-online' : ''}">
            <div class="presence-avatar ${isAdm ? 'adm' : ''}">
              ${avatarHtml}
              <span class="presence-dot ${u.isOnline ? 'on' : 'off'}"></span>
            </div>
            <div class="presence-info">
              <div class="presence-name-row">
                <span class="presence-name">${displayName}</span>
                ${statusBadge}
              </div>
              <div class="presence-meta">
                <span class="presence-role-pill ${isAdm ? 'adm' : ''}">${roleText}</span> &bull; 
                <span class="presence-plat-pill">${platformIcon}</span>
              </div>
            </div>
            <div class="presence-actions">
              <button class="presence-btn-chat" onclick="window.Chat.tagUser('${displayName}')" title="Nhắn tin cho ${displayName}">
                💬 Nhắn tin
              </button>
            </div>
          </div>
        `;
      }).join('');

      container.innerHTML = html;
    }

    // =========================================================================
    // 2. REALTIME TEAM MESSAGES (TRÒ CHUYỆN NỘI BỘ)
    // =========================================================================

    _initRealtimeStream() {
      try {
        if (this._eventSource) {
          this._eventSource.close();
          this._eventSource = null;
        }
        const base = this.getFirebaseBase();
        if (!base) return;

        this._eventSource = new EventSource(`${base}/team_messages.json`);
        this._eventSource.addEventListener('put', (e) => {
          try {
            if (!e.data) return;
            const payload = JSON.parse(e.data);
            if (!payload) return;

            if (payload.path === '/') {
              if (payload.data && typeof payload.data === 'object') {
                this._processIncomingData(payload.data, false);
              }
            } else if (payload.path && payload.data) {
              const key = payload.path.replace(/^\//, '');
              this._handleSingleNewMessage({ key, ...payload.data });
            }
          } catch (_) {}
        });

        this._eventSource.onerror = () => {
          // EventSource tự động reconnect; polling nền vẫn chạy bình thường
        };
      } catch (_) {}
    }

    _handleSingleNewMessage(newMsg) {
      if (!newMsg || (!newMsg.text && !newMsg.photoUrl && !newMsg.audioUrl)) return;

      const currentUser = this.getCurrentUser();
      const curUname = (currentUser?.username || '').toLowerCase().trim();
      const msgUname = (newMsg.username || '').toLowerCase().trim();
      const isFromOther = msgUname && msgUname !== curUname;

      const existingIdx = this.messages.findIndex(m => (m.key && m.key === newMsg.key) || (m.id && newMsg.id && m.id === newMsg.id));
      if (existingIdx !== -1) {
        this.messages[existingIdx] = Object.assign({}, this.messages[existingIdx], newMsg);
      } else {
        this.messages.push(newMsg);
        this.messages.sort((a, b) => new Date(a.createdAt || 0) - new Date(b.createdAt || 0));
        if (this.messages.length > 80) this.messages = this.messages.slice(-80);

        if (isFromOther) {
          if (newMsg.isAlert) {
            this.playEmergencySiren();
            this.showChatToast(newMsg, true);
          } else if (!this.isOpen) {
            this.unreadCount++;
            this.updateUnreadBadge();
            this.playChime();
            this.showChatToast(newMsg, false);
          } else {
            this.playChime();
          }
        }
      }

      this._lastSeenMsgKey = newMsg.key || this._lastSeenMsgKey;
      this.renderMessages();
      if (this.isOpen) {
        this.scrollToBottom();
      }
    }

    _processIncomingData(data, isFirstLoad = false) {
      if (!data || typeof data !== 'object') return;
      let list = Object.entries(data).map(([k, v]) => ({ key: k, ...v }));
      list.sort((a, b) => new Date(a.createdAt || 0) - new Date(b.createdAt || 0));
      if (list.length > 80) list = list.slice(-80);

      const currentUser = this.getCurrentUser();
      const curUname = (currentUser?.username || '').toLowerCase().trim();

      // Kiểm tra tin nhắn mới từ người khác
      if (!isFirstLoad && list.length > 0) {
        const lastMsg = list[list.length - 1];
        const msgUname = (lastMsg.username || '').toLowerCase().trim();
        if (lastMsg.key && lastMsg.key !== this._lastSeenMsgKey && msgUname !== curUname) {
          this._lastSeenMsgKey = lastMsg.key;
          if (lastMsg.isAlert) {
            this.playEmergencySiren();
            this.showChatToast(lastMsg, true);
          } else if (!this.isOpen) {
            this.unreadCount++;
            this.updateUnreadBadge();
            this.playChime();
            this.showChatToast(lastMsg, false);
          } else {
            this.playChime();
          }
        }
      }

      if (list.length > 0) {
        this._lastSeenMsgKey = list[list.length - 1].key;
      }

      // Giữ lại các tin nhắn optimistic chưa kịp đồng bộ (nếu có)
      const existingOptimistic = this.messages.filter(m => !m.key && m.id && !list.some(s => s.id === m.id));
      if (existingOptimistic.length > 0) {
        list = [...list, ...existingOptimistic];
        list.sort((a, b) => new Date(a.createdAt || 0) - new Date(b.createdAt || 0));
      }

      this.messages = list;
      this.renderMessages();
      if (this.isOpen) {
        this.scrollToBottom();
      }
    }

    async fetchMessages(isFirstLoad = false) {
      const base = this.getFirebaseBase();
      try {
        const res = await fetch(`${base}/team_messages.json`).catch(() => null);
        if (res && res.ok) {
          const data = await res.json();
          this._processIncomingData(data, isFirstLoad);
        }
      } catch (_) {}
    }

    async sendMessage(text, options = {}) {
      text = String(text || '').trim();
      if (!text && !options.photoUrl && !options.audioUrl) return;

      const user = this.getCurrentUser();
      const base = this.getFirebaseBase();
      const isAdmUser = user.role === 'admin' || (user.username || '').toLowerCase() === 'kha' || (user.name && user.name.toLowerCase().includes('admin'));
      const senderDisplayName = isAdmUser ? 'Admin' : this.fixViText(user.name || user.username);

      const timeStr = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
      const msgObj = {
        id: 'msg_' + Date.now().toString(36) + Math.random().toString(36).substr(2, 4),
        username: user.username,
        senderName: senderDisplayName,
        role: isAdmUser ? 'admin' : (user.role || 'engineer'),
        avatarUrl: user.avatarUrl || null,
        platform: 'desktop',
        text: text || (options.photoUrl ? '📸 Ảnh hiện trường' : (options.audioUrl ? '🎙️ Tin nhắn bộ đàm' : '')),
        photoUrl: options.photoUrl || null,
        audioUrl: options.audioUrl || null,
        audioDuration: options.audioDuration || null,
        isAlert: options.isAlert || false,
        alertType: options.alertType || null,
        timestamp: timeStr,
        createdAt: new Date().toISOString()
      };

      // Âm thanh
      if (options.isAlert) {
        this.playEmergencySiren();
      } else {
        this.playSendSound();
      }

      // Đẩy lạc quan (Optimistic UI) vào danh sách hiển thị ngay lập tức
      this.messages.push(msgObj);
      this.renderMessages();
      this.scrollToBottom();

      try {
        const res = await fetch(`${base}/team_messages.json`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json; charset=utf-8' },
          body: JSON.stringify(msgObj)
        });
        if (res && res.ok) {
          const resData = await res.json();
          if (resData && resData.name) {
            msgObj.key = resData.name;
            this._lastSeenMsgKey = resData.name;
          }
        }
      } catch (e) {
        console.warn('Lỗi gửi tin nhắn:', e);
      }
    }

    // =========================================================================
    // MULTIMEDIA: ẢNH HIỆN TRƯỜNG, BỘ ĐÀM THOẠI & CẢNH BÁO SOS
    // =========================================================================
    handlePhotoSelect(event) {
      const file = event.target.files && event.target.files[0];
      if (!file) return;
      event.target.value = ''; // Reset file input

      const reader = new FileReader();
      reader.onload = (e) => {
        const img = new Image();
        img.onload = () => {
          const canvas = document.createElement('canvas');
          let w = img.width;
          let h = img.height;
          const maxDim = 720;
          if (w > maxDim || h > maxDim) {
            if (w > h) {
              h = Math.round((h * maxDim) / w);
              w = maxDim;
            } else {
              w = Math.round((w * maxDim) / h);
              h = maxDim;
            }
          }
          canvas.width = w;
          canvas.height = h;
          const ctx = canvas.getContext('2d');
          ctx.drawImage(img, 0, 0, w, h);
          const compressedDataUrl = canvas.toDataURL('image/jpeg', 0.72);

          const caption = prompt('Nhập ghi chú cho ảnh hiện trường (hoặc bấm OK để gửi ngay):', '');
          this.sendMessage(caption || '📸 Ảnh hiện trường gửi về', { photoUrl: compressedDataUrl });
        };
        img.src = e.target.result;
      };
      reader.readAsDataURL(file);
    }

    async toggleVoiceRecording() {
      if (this._isRecording) {
        this.stopVoiceRecording(true);
      } else {
        await this.startVoiceRecording();
      }
    }

    async startVoiceRecording() {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        this._audioChunks = [];
        this._mediaRecorder = new MediaRecorder(stream);
        this._mediaRecorder.ondataavailable = (e) => {
          if (e.data && e.data.size > 0) this._audioChunks.push(e.data);
        };
        this._mediaRecorder.start();
        this._isRecording = true;
        this._recSeconds = 0;

        const micBtn = document.getElementById('kchat-btn-mic');
        if (micBtn) micBtn.classList.add('recording');
        const recBar = document.getElementById('kchat-recording-bar');
        if (recBar) recBar.style.display = 'flex';
        const timerEl = document.getElementById('kchat-rec-timer');
        if (timerEl) timerEl.textContent = '00:00';

        this._recTimer = setInterval(() => {
          this._recSeconds++;
          const m = String(Math.floor(this._recSeconds / 60)).padStart(2, '0');
          const s = String(this._recSeconds % 60).padStart(2, '0');
          if (timerEl) timerEl.textContent = `${m}:${s}`;
          if (this._recSeconds >= 60) {
            this.stopVoiceRecording(true);
          }
        }, 1000);

        this.playBeep(640, 0.08);
      } catch (err) {
        alert('Không thể truy cập Microphone để ghi âm bộ đàm: ' + err.message);
      }
    }

    stopVoiceRecording(shouldSend = true) {
      if (!this._isRecording || !this._mediaRecorder) return;
      this._isRecording = false;
      if (this._recTimer) clearInterval(this._recTimer);

      const micBtn = document.getElementById('kchat-btn-mic');
      if (micBtn) micBtn.classList.remove('recording');
      const recBar = document.getElementById('kchat-recording-bar');
      if (recBar) recBar.style.display = 'none';

      const duration = this._recSeconds;

      this._mediaRecorder.onstop = () => {
        if (shouldSend && this._audioChunks.length > 0) {
          const blob = new Blob(this._audioChunks, { type: 'audio/webm' });
          const reader = new FileReader();
          reader.onloadend = () => {
            const base64Audio = reader.result;
            this.sendMessage('🎙️ Tin nhắn bộ đàm thoại', {
              audioUrl: base64Audio,
              audioDuration: Math.max(1, duration)
            });
          };
          reader.readAsDataURL(blob);
        }
        try {
          this._mediaRecorder.stream.getTracks().forEach(t => t.stop());
        } catch (_) {}
      };

      try {
        this._mediaRecorder.stop();
      } catch (_) {}
    }

    togglePlayAudio(el, audioUrl) {
      if (this._activeAudio && this._activeAudioBtn === el) {
        if (!this._activeAudio.paused) {
          this._activeAudio.pause();
          el.classList.remove('playing');
          const btn = el.querySelector('.kchat-voice-play-btn');
          if (btn) btn.textContent = '▶️';
          return;
        } else {
          this._activeAudio.play();
          el.classList.add('playing');
          const btn = el.querySelector('.kchat-voice-play-btn');
          if (btn) btn.textContent = '⏸️';
          return;
        }
      }

      if (this._activeAudio) {
        this._activeAudio.pause();
        if (this._activeAudioBtn) {
          this._activeAudioBtn.classList.remove('playing');
          const prevBtn = this._activeAudioBtn.querySelector('.kchat-voice-play-btn');
          if (prevBtn) prevBtn.textContent = '▶️';
        }
      }

      const audio = new Audio(audioUrl);
      this._activeAudio = audio;
      this._activeAudioBtn = el;

      el.classList.add('playing');
      const btn = el.querySelector('.kchat-voice-play-btn');
      if (btn) btn.textContent = '⏸️';

      audio.onended = () => {
        el.classList.remove('playing');
        if (btn) btn.textContent = '▶️';
        this._activeAudio = null;
        this._activeAudioBtn = null;
      };

      audio.onerror = () => {
        el.classList.remove('playing');
        if (btn) btn.textContent = '▶️';
        this._activeAudio = null;
        this._activeAudioBtn = null;
      };

      audio.play().catch(() => {});
    }

    playEmergencySiren() {
      try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sawtooth';
        osc.frequency.setValueAtTime(750, ctx.currentTime);
        osc.frequency.linearRampToValueAtTime(1250, ctx.currentTime + 0.3);
        osc.frequency.linearRampToValueAtTime(750, ctx.currentTime + 0.6);
        osc.frequency.linearRampToValueAtTime(1250, ctx.currentTime + 0.9);
        osc.frequency.linearRampToValueAtTime(750, ctx.currentTime + 1.2);
        gain.gain.setValueAtTime(0.28, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 1.5);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start();
        osc.stop(ctx.currentTime + 1.5);
      } catch (_) {}
    }

    promptSosAlert() {
      const reason = prompt('🚨 BÁO ĐỘNG SỰ CỐ KHẨN CẤP HIỆN TRƯỜNG\n\nNhập nội dung sự cố (Ví dụ: Dừng máy E-Stop, Lỗi Robot va chạm, Chập điện tủ điều khiển):', 'Dừng khẩn cấp E-Stop / Sự cố dây chuyền');
      if (!reason) return;

      this.sendMessage(reason.trim(), {
        isAlert: true,
        alertType: 'SOS'
      });
      this.playEmergencySiren();
    }

    openPhotoLightbox(url) {
      let lb = document.getElementById('kchat-lightbox');
      if (!lb) {
        lb = document.createElement('div');
        lb.id = 'kchat-lightbox';
        lb.style.cssText = 'position:fixed; inset:0; background:rgba(0,0,0,0.85); backdrop-filter:blur(6px); z-index:99999; display:flex; align-items:center; justify-content:center; cursor:pointer;';
        lb.onclick = () => { lb.style.display = 'none'; };
        lb.innerHTML = `
          <div style="position:relative; max-width:90vw; max-height:90vh;" onclick="event.stopPropagation()">
            <img id="kchat-lightbox-img" style="max-width:90vw; max-height:85vh; object-fit:contain; border-radius:8px; box-shadow:0 10px 40px rgba(0,0,0,0.8);">
            <div style="display:flex; justify-content:flex-end; gap:8px; margin-top:8px;">
              <a id="kchat-lightbox-dl" class="btn" href="" download target="_blank" style="padding:6px 14px; background:#0284c7; color:#fff; border-radius:6px; text-decoration:none; font-size:12px;">Tải Về</a>
              <button class="btn" onclick="document.getElementById('kchat-lightbox').style.display='none'" style="padding:6px 14px; background:#334155; color:#fff; border-radius:6px; border:none; cursor:pointer; font-size:12px;">Đóng</button>
            </div>
          </div>
        `;
        document.body.appendChild(lb);
      }
      document.getElementById('kchat-lightbox-img').src = url;
      document.getElementById('kchat-lightbox-dl').href = url;
      lb.style.display = 'flex';
    }

    playBeep(freq = 520, duration = 0.08) {
      try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.frequency.setValueAtTime(freq, ctx.currentTime);
        gain.gain.setValueAtTime(0.12, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + duration);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start();
        osc.stop(ctx.currentTime + duration);
      } catch (_) {}
    }

    playSendSound() {
      try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sine';
        osc.frequency.setValueAtTime(520, ctx.currentTime);
        osc.frequency.exponentialRampToValueAtTime(1040, ctx.currentTime + 0.07);
        gain.gain.setValueAtTime(0.12, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.1);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start();
        osc.stop(ctx.currentTime + 0.1);
      } catch (_) {}
    }

    toggleEmojiPicker() {
      const picker = document.getElementById('kchat-emoji-picker');
      if (picker) {
        const isHidden = picker.style.display === 'none' || !picker.style.display;
        picker.style.display = isHidden ? 'flex' : 'none';
      }
    }

    insertEmoji(emoji) {
      const input = document.getElementById('kchat-input');
      if (input) {
        input.value = (input.value || '') + emoji;
        input.focus();
      }
      const picker = document.getElementById('kchat-emoji-picker');
      if (picker) picker.style.display = 'none';
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
        const isAdm = m.role === 'admin' || (m.username || '').toLowerCase() === 'kha' || (m.senderName && m.senderName.toLowerCase().includes('kha'));
        const roleBadge = isAdm ? '👑 Admin' : '👷 Kỹ sư';
        const platformBadge = m.platform === 'mobile' ? '📱 Mobile' : '💻 PC';

        const msgDate = m.createdAt ? new Date(m.createdAt).toLocaleDateString('vi-VN') : '';
        if (msgDate && msgDate !== lastDateStr) {
          lastDateStr = msgDate;
          html += `<div class="kchat-date-divider"><span>${msgDate}</span></div>`;
        }

        const senderName = isAdm ? 'Admin' : this.escapeHtml(this.fixViText(m.senderName || m.username));
        const safeUname = this.escapeHtml(m.username || '');
        const senderAvatarUrl = m.avatarUrl || (this.presenceMap && this.presenceMap[(m.username || '').replace(/\./g, '_')]?.avatarUrl) || null;
        const msgAvatarContent = senderAvatarUrl
          ? `<img src="${senderAvatarUrl}" class="kchat-msg-avatar-img" alt="${senderName}">`
          : `<span>${(m.senderName || m.username || 'U').charAt(0).toUpperCase()}</span>`;

        html += `
          <div class="kchat-msg-row ${isMe ? 'is-me' : 'is-other'}">
            ${!isMe ? `
              <div class="kchat-msg-avatar ${isAdm ? 'adm' : ''}" title="${senderName} (${roleBadge})">
                ${msgAvatarContent}
                <span class="kchat-msg-plat-ico">${m.platform === 'mobile' ? '📱' : '💻'}</span>
              </div>
            ` : ''}
            <div class="kchat-bubble-wrap">
              ${!isMe ? `
                <div class="kchat-sender-info">
                  <strong class="kchat-sender-name">${senderName}</strong>
                  <span class="kchat-role-tag ${isAdm ? 'adm' : ''}">${roleBadge}</span>
                </div>
              ` : ''}
              <div class="kchat-bubble ${isMe ? 'me' : 'other'} ${m.isAlert ? 'kchat-alert-card' : ''}">
                ${m.isAlert ? `
                  <div class="kchat-alert-title">
                    <span>🚨 BÁO ĐỘNG SỰ CỐ HIỆN TRƯỜNG</span>
                  </div>
                ` : ''}
                <div class="kchat-text">${this.formatMessageText(m.text)}</div>
                ${m.photoUrl ? `
                  <div class="kchat-photo-thumb" onclick="window.Chat.openPhotoLightbox('${m.photoUrl}')" title="Bấm để xem ảnh lớn">
                    <img src="${m.photoUrl}" alt="Ảnh hiện trường">
                  </div>
                ` : ''}
                ${m.audioUrl ? `
                  <div class="kchat-voice-memo" onclick="window.Chat.togglePlayAudio(this, '${m.audioUrl}')" title="Bấm để nghe tin nhắn bộ đàm">
                    <button type="button" class="kchat-voice-play-btn">▶️</button>
                    <div class="kchat-voice-waves">
                      <span class="vbar"></span><span class="vbar"></span><span class="vbar"></span><span class="vbar"></span><span class="vbar"></span>
                    </div>
                    <span class="kchat-voice-duration">${m.audioDuration ? m.audioDuration + 's' : '00:03'}</span>
                  </div>
                ` : ''}
                <div class="kchat-time">
                  <span>${this.escapeHtml(m.timestamp || '')}</span>
                  ${isMe ? `<span class="kchat-ticks" title="Đã gửi lên Cloud">✓✓</span>` : ''}
                </div>
              </div>
              <!-- Thanh tương tác nhanh khi hover -->
              <div class="kchat-hover-actions">
                <button type="button" class="kchat-react-btn" onclick="window.Chat.sendQuick('👍 @${senderName}')" title="Thích">👍</button>
                <button type="button" class="kchat-react-btn" onclick="window.Chat.sendQuick('❤️ @${senderName}')" title="Thả tim">❤️</button>
                <button type="button" class="kchat-react-btn" onclick="window.Chat.sendQuick('⚡ @${senderName}')" title="Xử lý ngay">⚡</button>
                <button type="button" class="kchat-react-btn reply" onclick="window.Chat.tagUser('${senderName}')" title="Trả lời">💬 Trả lời</button>
              </div>
            </div>
            ${isMe ? `
              <div class="kchat-msg-avatar is-me ${isAdm ? 'adm' : ''}" title="Bạn (${senderName})">
                ${msgAvatarContent}
              </div>
            ` : ''}
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

