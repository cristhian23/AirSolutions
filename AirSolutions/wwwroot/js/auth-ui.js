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
      '          <h2 class="modal-title fs-5">Iniciar sesion</h2>',
      '          <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Cerrar"></button>',
      '        </div>',
      '        <div class="modal-body">',
      '          <div class="mb-3">',
      '            <label class="form-label" for="globalAssistantUser">Usuario</label>',
      '            <input id="globalAssistantUser" class="form-control" type="text" required>',
      '          </div>',
      '          <div>',
      '            <label class="form-label" for="globalAssistantPass">Contrasena</label>',
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
      welcomeText.textContent = 'Bienvenido "' + displayName + '"';
    } else {
      welcomeText.textContent = '';
    }
  }

  const loginModalElement = ensureLoginModal();
  const loginModal = new bootstrap.Modal(loginModalElement);
  const loginForm = document.getElementById('globalLoginForm');
  const userInput = document.getElementById('globalAssistantUser');
  const passInput = document.getElementById('globalAssistantPass');
  const loginResult = document.getElementById('globalLoginResult');

  async function login() {
    const username = userInput.value.trim();
    const password = passInput.value.trim();
    if (!username || !password) {
      loginResult.textContent = 'Completa usuario y contrasena.';
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
        loginResult.textContent = (data && data.message) || 'No se pudo iniciar sesion.';
        return;
      }

      setSession(data.accessToken, {
        username: data.username || username,
        fullName: data.fullName || data.username || username,
        role: data.role || 'User'
      });

      passInput.value = '';
      loginModal.hide();
      updateAuthUi();
    } catch {
      loginResult.textContent = 'Error de conexion al iniciar sesion.';
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
  });

  updateAuthUi();
})();
