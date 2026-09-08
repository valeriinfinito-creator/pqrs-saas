(function () {
  'use strict';

  const scriptTag = document.currentScript;
  const tenantApiKey = scriptTag.getAttribute('data-tenant');
  const apiBaseUrl = scriptTag.getAttribute('data-api-url') || 'http://localhost:5219';
  const companyName = scriptTag.getAttribute('data-company-name') || 'Soporte';
  const primaryColor = scriptTag.getAttribute('data-primary-color') || '#4f46e5';

  if (!tenantApiKey) {
    console.error('[PQRS Widget] Falta el atributo data-tenant en el <script> tag.');
    return;
  }

  const STORAGE_KEY = 'pqrs_chat_' + tenantApiKey;

  const host = document.createElement('div');
  host.id = 'pqrs-widget-host';
  document.body.appendChild(host);

  const shadow = host.attachShadow({ mode: 'open' });

  const style = document.createElement('style');
  style.textContent = `
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');

    :host, * {
      box-sizing: border-box;
      font-family: 'Inter', system-ui, -apple-system, sans-serif;
    }

    :host {
      --pqrs-primary: ${primaryColor};
      --pqrs-primary-dark: color-mix(in srgb, ${primaryColor} 80%, black);
      --pqrs-gradient: linear-gradient(135deg, ${primaryColor} 0%, color-mix(in srgb, ${primaryColor} 60%, #7c3aed) 100%);
    }

    /* --- Botón flotante --- */
    .pqrs-fab-wrap {
      position: fixed;
      bottom: 24px;
      right: 24px;
      z-index: 999999;
    }
    .pqrs-fab {
      position: relative;
      width: 62px;
      height: 62px;
      border-radius: 50%;
      background: var(--pqrs-gradient);
      color: white;
      border: none;
      cursor: pointer;
      box-shadow: 0 6px 20px rgba(0,0,0,0.25), 0 0 0 0 rgba(79,70,229,0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
      animation: pqrs-pulse 2.5s infinite;
    }
    .pqrs-fab:hover { transform: scale(1.08); }
    .pqrs-fab:active { transform: scale(0.96); }
    .pqrs-fab svg { width: 26px; height: 26px; }

    @keyframes pqrs-pulse {
      0% { box-shadow: 0 6px 20px rgba(0,0,0,0.25), 0 0 0 0 rgba(79,70,229,0.45); }
      70% { box-shadow: 0 6px 20px rgba(0,0,0,0.25), 0 0 0 14px rgba(79,70,229,0); }
      100% { box-shadow: 0 6px 20px rgba(0,0,0,0.25), 0 0 0 0 rgba(79,70,229,0); }
    }

    .pqrs-badge {
      position: absolute;
      top: -2px;
      right: -2px;
      width: 18px;
      height: 18px;
      background: #ef4444;
      border: 2px solid white;
      border-radius: 50%;
      display: none;
    }
    .pqrs-badge.pqrs-show { display: block; }

    /* --- Panel --- */
    .pqrs-panel {
      position: fixed;
      bottom: 100px;
      right: 24px;
      width: 380px;
      height: 560px;
      max-height: calc(100vh - 140px);
      background: #ffffff;
      border-radius: 20px;
      box-shadow: 0 20px 60px rgba(0,0,0,0.25);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      z-index: 999999;
      opacity: 0;
      transform: translateY(16px) scale(0.97);
      pointer-events: none;
      transition: opacity 0.25s ease, transform 0.25s ease;
    }
    .pqrs-panel.pqrs-open {
      opacity: 1;
      transform: translateY(0) scale(1);
      pointer-events: all;
    }

    .pqrs-header {
      background: var(--pqrs-gradient);
      color: white;
      padding: 18px 20px;
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .pqrs-avatar {
      width: 38px;
      height: 38px;
      border-radius: 50%;
      background: rgba(255,255,255,0.2);
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 15px;
      flex-shrink: 0;
    }
    .pqrs-header-text { flex: 1; min-width: 0; }
    .pqrs-header-title { font-weight: 600; font-size: 15px; }
    .pqrs-header-status {
      font-size: 12px;
      opacity: 0.85;
      display: flex;
      align-items: center;
      gap: 5px;
      margin-top: 2px;
    }
    .pqrs-status-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: #4ade80;
      box-shadow: 0 0 0 2px rgba(74,222,128,0.3);
    }
    .pqrs-close {
      background: rgba(255,255,255,0.15);
      border: none;
      color: white;
      width: 30px;
      height: 30px;
      border-radius: 50%;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      transition: background 0.15s ease;
    }
    .pqrs-close:hover { background: rgba(255,255,255,0.28); }
    .pqrs-close svg { width: 14px; height: 14px; }

    /* --- Mensajes --- */
    .pqrs-messages {
      flex: 1;
      padding: 18px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 4px;
      background: #f9fafb;
    }
    .pqrs-messages::-webkit-scrollbar { width: 6px; }
    .pqrs-messages::-webkit-scrollbar-thumb { background: #d1d5db; border-radius: 3px; }

    .pqrs-row {
      display: flex;
      flex-direction: column;
      margin-bottom: 10px;
      animation: pqrs-fade-in 0.25s ease;
    }
    @keyframes pqrs-fade-in {
      from { opacity: 0; transform: translateY(6px); }
      to { opacity: 1; transform: translateY(0); }
    }
    .pqrs-row-user { align-items: flex-end; }
    .pqrs-row-bot { align-items: flex-start; }

    .pqrs-msg {
      max-width: 82%;
      padding: 11px 15px;
      border-radius: 14px;
      font-size: 14px;
      line-height: 1.45;
    }
    .pqrs-msg-bot {
      background: white;
      color: #1f2937;
      border: 1px solid #e5e7eb;
      border-bottom-left-radius: 4px;
    }
    .pqrs-msg-user {
      background: var(--pqrs-gradient);
      color: white;
      border-bottom-right-radius: 4px;
    }
    .pqrs-time {
      font-size: 10.5px;
      color: #9ca3af;
      margin-top: 4px;
      padding: 0 4px;
    }

    .pqrs-typing {
      display: flex;
      gap: 4px;
      padding: 13px 16px;
      background: white;
      border: 1px solid #e5e7eb;
      border-radius: 14px;
      border-bottom-left-radius: 4px;
      width: fit-content;
    }
    .pqrs-typing span {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: #9ca3af;
      animation: pqrs-bounce 1.2s infinite ease-in-out;
    }
    .pqrs-typing span:nth-child(2) { animation-delay: 0.15s; }
    .pqrs-typing span:nth-child(3) { animation-delay: 0.3s; }
    @keyframes pqrs-bounce {
      0%, 60%, 100% { transform: translateY(0); opacity: 0.5; }
      30% { transform: translateY(-5px); opacity: 1; }
    }

    .pqrs-feedback {
      display: flex;
      gap: 8px;
      margin-top: 8px;
      margin-left: 2px;
    }
    .pqrs-feedback button {
      padding: 7px 13px;
      border-radius: 20px;
      border: 1px solid #e5e7eb;
      background: white;
      cursor: pointer;
      font-size: 12.5px;
      font-weight: 500;
      color: #374151;
      transition: all 0.15s ease;
    }
    .pqrs-feedback button:hover {
      border-color: var(--pqrs-primary);
      color: var(--pqrs-primary);
      background: color-mix(in srgb, ${primaryColor} 6%, white);
    }

    /* --- Input --- */
    .pqrs-input-row {
      display: flex;
      gap: 8px;
      padding: 14px;
      border-top: 1px solid #e5e7eb;
      background: white;
    }
    .pqrs-input-row input {
      flex: 1;
      padding: 11px 14px;
      border: 1.5px solid #e5e7eb;
      border-radius: 24px;
      font-size: 14px;
      outline: none;
      transition: border-color 0.15s ease;
    }
    .pqrs-input-row input:focus { border-color: var(--pqrs-primary); }
    .pqrs-send {
      background: var(--pqrs-gradient);
      color: white;
      border: none;
      border-radius: 50%;
      width: 42px;
      height: 42px;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      transition: transform 0.15s ease, opacity 0.15s ease;
    }
    .pqrs-send:hover { transform: scale(1.06); }
    .pqrs-send:disabled { opacity: 0.5; cursor: not-allowed; transform: none; }
    .pqrs-send svg { width: 17px; height: 17px; }

    /* --- Formulario --- */
    .pqrs-form {
      display: none;
      flex-direction: column;
      gap: 12px;
      padding: 18px;
      overflow-y: auto;
      background: #f9fafb;
    }
    .pqrs-form-intro {
      font-size: 13px;
      color: #6b7280;
      margin-bottom: 2px;
      line-height: 1.4;
    }
    .pqrs-field { display: flex; flex-direction: column; gap: 5px; }
    .pqrs-field label {
      font-size: 12.5px;
      font-weight: 600;
      color: #374151;
    }
    .pqrs-field input, .pqrs-field textarea {
      padding: 11px 13px;
      border: 1.5px solid #e5e7eb;
      border-radius: 10px;
      font-size: 14px;
      outline: none;
      background: white;
      transition: border-color 0.15s ease;
      font-family: inherit;
    }
    .pqrs-field input:focus, .pqrs-field textarea:focus { border-color: var(--pqrs-primary); }
    .pqrs-field.pqrs-invalid input, .pqrs-field.pqrs-invalid textarea { border-color: #ef4444; }
    .pqrs-field-error {
      font-size: 11.5px;
      color: #ef4444;
      display: none;
    }
    .pqrs-field.pqrs-invalid .pqrs-field-error { display: block; }
    .pqrs-form textarea { resize: none; }

    .pqrs-form-actions {
      display: flex;
      gap: 8px;
      margin-top: 4px;
    }
    .pqrs-btn-back {
      background: white;
      color: #374151;
      border: 1.5px solid #e5e7eb;
      border-radius: 10px;
      padding: 11px 16px;
      cursor: pointer;
      font-size: 13.5px;
      font-weight: 500;
      flex: 1;
    }
    .pqrs-btn-back:hover { background: #f3f4f6; }
    .pqrs-btn-submit {
      background: var(--pqrs-gradient);
      color: white;
      border: none;
      border-radius: 10px;
      padding: 11px 16px;
      cursor: pointer;
      font-size: 13.5px;
      font-weight: 600;
      flex: 2;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      transition: opacity 0.15s ease;
    }
    .pqrs-btn-submit:disabled { opacity: 0.6; cursor: not-allowed; }
    .pqrs-spinner {
      width: 14px;
      height: 14px;
      border: 2px solid rgba(255,255,255,0.4);
      border-top-color: white;
      border-radius: 50%;
      animation: pqrs-spin 0.7s linear infinite;
      display: none;
    }
    .pqrs-btn-submit.pqrs-loading .pqrs-spinner { display: block; }
    @keyframes pqrs-spin { to { transform: rotate(360deg); } }
  `;
  shadow.appendChild(style);

  const initials = companyName.trim().charAt(0).toUpperCase() || 'S';

  // --- Botón flotante ---
  const fabWrap = document.createElement('div');
  fabWrap.className = 'pqrs-fab-wrap';
  fabWrap.innerHTML = `
    <button class="pqrs-fab" aria-label="Abrir chat de soporte">
      <span class="pqrs-badge"></span>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"/>
      </svg>
    </button>
  `;
  shadow.appendChild(fabWrap);
  const fab = fabWrap.querySelector('.pqrs-fab');
  const badge = fabWrap.querySelector('.pqrs-badge');

  // --- Panel ---
  const panel = document.createElement('div');
  panel.className = 'pqrs-panel';
  panel.innerHTML = `
    <div class="pqrs-header">
      <div class="pqrs-avatar">${initials}</div>
      <div class="pqrs-header-text">
        <div class="pqrs-header-title">${companyName}</div>
        <div class="pqrs-header-status"><span class="pqrs-status-dot"></span>En línea</div>
      </div>
      <button class="pqrs-close" aria-label="Cerrar chat">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>
      </button>
    </div>
    <div class="pqrs-messages"></div>
    <div class="pqrs-input-row">
      <input type="text" placeholder="Escribe tu pregunta..." />
      <button class="pqrs-send" aria-label="Enviar">
        <svg viewBox="0 0 24 24" fill="currentColor"><path d="M2 21l21-9L2 3v7l15 2-15 2v7z"/></svg>
      </button>
    </div>
    <form class="pqrs-form">
      <div class="pqrs-form-intro">No encontramos una respuesta automática. Cuéntanos más y un agente te ayudará.</div>
      <div class="pqrs-field" data-field="customerName">
        <label>Nombre</label>
        <input type="text" name="customerName" placeholder="Tu nombre completo" />
        <span class="pqrs-field-error">Este campo es obligatorio</span>
      </div>
      <div class="pqrs-field" data-field="customerEmail">
        <label>Correo electrónico</label>
        <input type="email" name="customerEmail" placeholder="tucorreo@ejemplo.com" />
        <span class="pqrs-field-error">Ingresa un correo válido</span>
      </div>
      <div class="pqrs-field" data-field="subject">
        <label>Asunto</label>
        <input type="text" name="subject" placeholder="Resumen breve de tu solicitud" />
        <span class="pqrs-field-error">Este campo es obligatorio</span>
      </div>
      <div class="pqrs-field" data-field="description">
        <label>Descripción</label>
        <textarea name="description" rows="4" placeholder="Cuéntanos con detalle qué sucede..."></textarea>
        <span class="pqrs-field-error">Este campo es obligatorio</span>
      </div>
      <div class="pqrs-form-actions">
        <button type="button" class="pqrs-btn-back">Volver al chat</button>
        <button type="submit" class="pqrs-btn-submit">
          <span class="pqrs-spinner"></span>
          <span class="pqrs-btn-submit-text">Enviar solicitud</span>
        </button>
      </div>
    </form>
  `;
  shadow.appendChild(panel);

  const closeBtn = panel.querySelector('.pqrs-close');
  const messagesEl = panel.querySelector('.pqrs-messages');
  const inputRow = panel.querySelector('.pqrs-input-row');
  const inputEl = panel.querySelector('.pqrs-input-row input');
  const sendBtn = panel.querySelector('.pqrs-input-row .pqrs-send');
  const formEl = panel.querySelector('.pqrs-form');
  const backBtn = panel.querySelector('.pqrs-btn-back');
  const submitBtn = panel.querySelector('.pqrs-btn-submit');

  let unreadCount = 0;
  let isOpen = false;

  // --- Persistencia de sesión (misma pestaña) ---
  function loadHistory() {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch { return []; }
  }
  function saveHistory(history) {
    try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(history)); } catch {}
  }
  let history = loadHistory();

  function formatTime(date) {
    return date.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' });
  }

  function renderMessage(sender, text, time, persist = true) {
    const row = document.createElement('div');
    row.className = `pqrs-row pqrs-row-${sender}`;
    const bubble = document.createElement('div');
    bubble.className = `pqrs-msg pqrs-msg-${sender}`;
    bubble.textContent = text;
    const timeEl = document.createElement('div');
    timeEl.className = 'pqrs-time';
    timeEl.textContent = time;
    row.appendChild(bubble);
    row.appendChild(timeEl);
    messagesEl.appendChild(row);
    messagesEl.scrollTop = messagesEl.scrollHeight;

    if (persist) {
      history.push({ sender, text, time });
      saveHistory(history);
    }

    if (sender === 'bot' && !isOpen) {
      unreadCount++;
      badge.classList.add('pqrs-show');
    }

    return row;
  }

  function addMessage(sender, text) {
    return renderMessage(sender, text, formatTime(new Date()));
  }

  function restoreHistory() {
    if (history.length === 0) {
      addMessage('bot', `¡Hola! 👋 Soy el asistente virtual de ${companyName}. ¿En qué puedo ayudarte hoy?`);
      return;
    }
    history.forEach(m => renderMessage(m.sender, m.text, m.time, false));
  }
  restoreHistory();

  // --- Abrir/cerrar panel ---
  function openPanel() {
    panel.classList.add('pqrs-open');
    isOpen = true;
    unreadCount = 0;
    badge.classList.remove('pqrs-show');
    setTimeout(() => inputEl.focus(), 200);
  }
  function closePanel() {
    panel.classList.remove('pqrs-open');
    isOpen = false;
  }

  fab.addEventListener('click', () => (isOpen ? closePanel() : openPanel()));
  closeBtn.addEventListener('click', closePanel);

  // --- Indicador de escribiendo ---
  function showTyping() {
    const row = document.createElement('div');
    row.className = 'pqrs-row pqrs-row-bot';
    row.innerHTML = `<div class="pqrs-typing"><span></span><span></span><span></span></div>`;
    messagesEl.appendChild(row);
    messagesEl.scrollTop = messagesEl.scrollHeight;
    return row;
  }

  // --- Envío de preguntas (RAG) ---
  async function sendQuestion() {
    const question = inputEl.value.trim();
    if (!question) return;

    addMessage('user', question);
    inputEl.value = '';
    sendBtn.disabled = true;

    const typingRow = showTyping();

    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/widget/rag-search`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Widget-Api-Key': tenantApiKey },
        body: JSON.stringify({ question })
      });

      if (!response.ok) throw new Error('HTTP ' + response.status);
      const data = await response.json();
      typingRow.remove();

      if (data.found) {
        addMessage('bot', data.answer);
        showFeedbackButtons(question);
      } else {
        addMessage('bot', 'No encontré información suficiente para responder eso. Te muestro el formulario para que un agente te ayude directamente.');
        setTimeout(() => showTicketForm(question), 700);
      }
    } catch (err) {
      typingRow.remove();
      addMessage('bot', 'Ocurrió un error al conectar con el servidor. Intenta de nuevo en un momento.');
      console.error('[PQRS Widget] Error en rag-search:', err);
    } finally {
      sendBtn.disabled = false;
    }
  }

  function showFeedbackButtons(originalQuestion) {
    const wrap = document.createElement('div');
    wrap.className = 'pqrs-feedback';
    wrap.innerHTML = `<button data-a="yes">👍 Sí, gracias</button><button data-a="no">👎 No resolvió</button>`;
    messagesEl.appendChild(wrap);
    messagesEl.scrollTop = messagesEl.scrollHeight;

    wrap.querySelector('[data-a="yes"]').addEventListener('click', () => {
      wrap.remove();
      addMessage('bot', '¡Genial! Si tienes otra duda, aquí estoy. 😊');
    });
    wrap.querySelector('[data-a="no"]').addEventListener('click', () => {
      wrap.remove();
      addMessage('bot', 'Entendido, vamos a conectarte con un agente.');
      setTimeout(() => showTicketForm(originalQuestion), 500);
    });
  }

  sendBtn.addEventListener('click', sendQuestion);
  inputEl.addEventListener('keydown', (e) => { if (e.key === 'Enter') sendQuestion(); });

  // --- Formulario ---
  function showTicketForm(originalQuestion) {
    messagesEl.style.display = 'none';
    inputRow.style.display = 'none';
    formEl.style.display = 'flex';
    if (originalQuestion) {
      formEl.querySelector('[name="subject"]').value = originalQuestion.slice(0, 80);
    }
  }
  function showChat() {
    formEl.style.display = 'none';
    messagesEl.style.display = 'flex';
    inputRow.style.display = 'flex';
  }
  backBtn.addEventListener('click', showChat);

  function validateField(name, isValid) {
    const field = formEl.querySelector(`[data-field="${name}"]`);
    field.classList.toggle('pqrs-invalid', !isValid);
    return isValid;
  }

  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  formEl.addEventListener('submit', async (e) => {
    e.preventDefault();

    const values = {
      customerName: formEl.customerName.value.trim(),
      customerEmail: formEl.customerEmail.value.trim(),
      subject: formEl.subject.value.trim(),
      description: formEl.description.value.trim()
    };

    const validName = validateField('customerName', values.customerName.length > 0);
    const validEmail = validateField('customerEmail', emailRegex.test(values.customerEmail));
    const validSubject = validateField('subject', values.subject.length > 0);
    const validDescription = validateField('description', values.description.length > 0);

    if (!validName || !validEmail || !validSubject || !validDescription) return;

    submitBtn.disabled = true;
    submitBtn.classList.add('pqrs-loading');
    submitBtn.querySelector('.pqrs-btn-submit-text').textContent = 'Enviando...';

    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/widget/tickets`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Widget-Api-Key': tenantApiKey },
        body: JSON.stringify({ ...values, originatedFromRag: true })
      });

      if (!response.ok) throw new Error('HTTP ' + response.status);
      const data = await response.json();

      formEl.reset();
      showChat();
      addMessage('bot', `¡Listo, ${values.customerName.split(' ')[0]}! Tu solicitud fue radicada con el número #${data.id.slice(0, 8).toUpperCase()}. Un agente la revisará muy pronto.`);
    } catch (err) {
      console.error('[PQRS Widget] Error creando ticket:', err);
      addMessage('bot', 'Ocurrió un error al enviar tu solicitud. Por favor intenta de nuevo.');
    } finally {
      submitBtn.disabled = false;
      submitBtn.classList.remove('pqrs-loading');
      submitBtn.querySelector('.pqrs-btn-submit-text').textContent = 'Enviar solicitud';
    }
  });

  console.log('[PQRS Widget] Cargado correctamente para tenant:', tenantApiKey);
})();