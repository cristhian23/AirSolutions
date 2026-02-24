(function () {
  const TOKEN_KEY = 'airsolutions_jwt';
  const USER_KEY = 'airsolutions_user';
  const API_AUTH = '/api/auth/login';

  const openLoginBtn = document.getElementById('openLoginBtn');
  const welcomeItem = document.getElementById('welcomeItem');
  const logoutItem = document.getElementById('logoutItem');
  const welcomeText = document.getElementById('welcomeText');
  const logoutBtn = document.getElementById('logoutBtn');

  if (!openLoginBtn || !welcomeItem || !logoutItem || !welcomeText || !logoutBtn) {
    return;
  }

  function getToken() {
    return localStorage.getItem(TOKEN_KEY);
  }

  function getStoredUser() {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }

  function setSession(token, user) {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user || {}));
  }

  function clearSession() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  function pickDisplayName(user) {
    if (!user) return '';
    const source = (user.fullName || user.username || '').trim();
    if (!source) return '';
    return source.split(/\s+/)[0];
  }

  function isAdminUser(user) {
    return !!user && String(user.role || '').toLowerCase() === 'admin';
  }

  function isModuleAdminPath(pathname) {
    return /^\/(clients|catalog-items|quotes|invoices)\//i.test(pathname || '');
  }

  function enforceAdminModuleAccess() {
    const pathname = window.location.pathname || '';
    if (!isModuleAdminPath(pathname)) {
      return false;
    }

    const token = getToken();
    const user = getStoredUser();
    if (token && isAdminUser(user)) {
      return false;
    }

    const next = window.location.pathname + window.location.search + window.location.hash;
    window.location.href = '/admin/?next=' + encodeURIComponent(next);
    return true;
  }

  function normalizeModuleHomeLinks() {
    if (!isModuleAdminPath(window.location.pathname || '')) {
      return;
    }

    document.querySelectorAll('a.nav-link[href="/"]').forEach(link => {
      const text = (link.textContent || '').trim().toLowerCase();
      if (text === 'inicio') {
        link.setAttribute('href', '/admin/');
      }
    });
  }

  function ensureLoginModal() {
    if (document.getElementById('globalLoginModal')) {
      return document.getElementById('globalLoginModal');
    }

    const wrapper = document.createElement('div');
    wrapper.innerHTML = [
      '<div class="modal fade" id="globalLoginModal" tabindex="-1" aria-hidden="true">',
      '  <div class="modal-dialog modal-dialog-centered">',
      '    <div class="modal-content">',
      '      <form id="globalLoginForm" novalidate>',
      '        <div class="modal-header">',
      '          <h2 class="modal-title fs-5">Iniciar sesión</h2>',
      '          <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Cerrar"></button>',
      '        </div>',
      '        <div class="modal-body">',
      '          <div class="mb-3">',
      '            <label class="form-label" for="globalAssistantUser">Usuario</label>',
      '            <input id="globalAssistantUser" class="form-control" type="text" required>',
      '          </div>',
      '          <div>',
      '            <label class="form-label" for="globalAssistantPass">Contraseña</label>',
      '            <input id="globalAssistantPass" class="form-control" type="password" required>',
      '          </div>',
      '          <div id="globalLoginResult" class="small mt-3 text-danger"></div>',
      '        </div>',
      '        <div class="modal-footer">',
      '          <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancelar</button>',
      '          <button type="submit" class="btn btn-brand">Entrar</button>',
      '        </div>',
      '      </form>',
      '    </div>',
      '  </div>',
      '</div>'
    ].join('');

    document.body.appendChild(wrapper.firstChild);
    return document.getElementById('globalLoginModal');
  }

  function updateAuthUi() {
    const token = getToken();
    const user = getStoredUser();
    const logged = !!token;

    openLoginBtn.classList.toggle('d-none', logged);
    welcomeItem.classList.toggle('d-none', !logged);
    logoutItem.classList.toggle('d-none', !logged);

    if (logged) {
      const displayName = pickDisplayName(user) || 'Usuario';
      welcomeText.textContent = 'Bienvenido ' + displayName + '';
    } else {
      welcomeText.textContent = '';
    }
  }

  function emitAuthEvent(logged, user) {
    try {
      window.dispatchEvent(new CustomEvent('airsolutions:auth', {
        detail: { loggedIn: logged, user: user || null }
      }));
    } catch {
      // no-op
    }
  }

  function refreshModuleData() {
    try {
      if (typeof window.airsolutionsRefresh === 'function') {
        window.airsolutionsRefresh();
      }
    } catch {
      // no-op
    }
  }

  const nativeFetch = window.fetch.bind(window);
  window.fetch = function (input, init) {
    try {
      const token = getToken();
      let url = '';
      if (typeof input === 'string') {
        url = input;
      } else if (input && typeof input.url === 'string') {
        url = input.url;
      }

      const isApiCall = /^\/api\//i.test(url) || /^https?:\/\/[^/]+\/api\//i.test(url);
      const isAuthLogin = /\/api\/auth\/login$/i.test(url);
      if (!isApiCall) {
        return nativeFetch(input, init);
      }

      if (!token) {
        if (!isAuthLogin) {
          promptLoginRequired('Inicia sesión para cargar los datos.');
        }
        return nativeFetch(input, init);
      }

      const requestInit = init ? { ...init } : {};
      const headers = new Headers(requestInit.headers || (input instanceof Request ? input.headers : undefined));
      if (!headers.has('Authorization')) {
        headers.set('Authorization', 'Bearer ' + token);
      }
      requestInit.headers = headers;

      return nativeFetch(input, requestInit).then(response => {
        if (response.status === 401) {
          clearSession();
          updateAuthUi();
          emitAuthEvent(false, null);
          if (!isAuthLogin) {
            promptLoginRequired('Tu sesión expiró. Inicia sesión nuevamente.');
          }
        }
        return response;
      });
    } catch {
      return nativeFetch(input, init);
    }
  };

  const loginModalElement = ensureLoginModal();
  const loginModal = new bootstrap.Modal(loginModalElement);
  const loginForm = document.getElementById('globalLoginForm');
  const userInput = document.getElementById('globalAssistantUser');
  const passInput = document.getElementById('globalAssistantPass');
  const loginResult = document.getElementById('globalLoginResult');
  let lastLoginPromptAt = 0;

  function promptLoginRequired(message) {
    const now = Date.now();
    if (now - lastLoginPromptAt < 1500) {
      return;
    }

    lastLoginPromptAt = now;
    loginResult.textContent = message || 'Debes iniciar sesión para ver la información.';
    loginModal.show();
  }

  if (enforceAdminModuleAccess()) {
    return;
  }

  normalizeModuleHomeLinks();

  let pendingAuthRefresh = false;

  loginModalElement.addEventListener('hidden.bs.modal', () => {
    if (!openLoginBtn.classList.contains('d-none')) {
      openLoginBtn.focus();
    } else if (!logoutBtn.classList.contains('d-none')) {
      logoutBtn.focus();
    }

    if (pendingAuthRefresh) {
      pendingAuthRefresh = false;
      emitAuthEvent(true, getStoredUser());
      refreshModuleData();
    }
  });

  async function login() {
    const username = userInput.value.trim();
    const password = passInput.value.trim();
    if (!username || !password) {
      loginResult.textContent = 'Completa usuario y contraseña.';
      return;
    }

    loginResult.textContent = '';
    try {
      const response = await fetch(API_AUTH, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
      });

      const data = await response.json();
      if (!response.ok || !data.accessToken) {
        loginResult.textContent = (data && data.message) || 'No se pudo iniciar sesión.';
        return;
      }

      setSession(data.accessToken, {
        username: data.username || username,
        fullName: data.fullName || data.username || username,
        role: data.role || 'User'
      });

      passInput.value = '';
      pendingAuthRefresh = true;
      loginModal.hide();
      updateAuthUi();
    } catch {
      loginResult.textContent = 'Error de conexión al iniciar sesión.';
    }
  }

  openLoginBtn.addEventListener('click', function () {
    loginResult.textContent = '';
    loginModal.show();
  });

  loginForm.addEventListener('submit', function (e) {
    e.preventDefault();
    login();
  });

  logoutBtn.addEventListener('click', function () {
    clearSession();
    updateAuthUi();
    emitAuthEvent(false, null);
    refreshModuleData();
    if (isModuleAdminPath(window.location.pathname || '')) {
      window.location.href = '/admin/';
    }
  });

  updateAuthUi();
})();

