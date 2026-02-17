const state = {
  editingId: null
};

const els = {
  alert: document.getElementById("alert"),
  formTitle: document.getElementById("formTitle"),
  form: document.getElementById("taskForm"),
  taskId: document.getElementById("taskId"),
  title: document.getElementById("title"),
  description: document.getElementById("description"),
  priority: document.getElementById("priority"),
  status: document.getElementById("status"),
  dueDate: document.getElementById("dueDate"),
  cancelEdit: document.getElementById("cancelEdit"),
  search: document.getElementById("search"),
  filterStatus: document.getElementById("filterStatus"),
  filterPriority: document.getElementById("filterPriority"),
  sort: document.getElementById("sort"),
  order: document.getElementById("order"),
  taskList: document.getElementById("taskList")
};

document.addEventListener("DOMContentLoaded", () => {
  M.AutoInit();
  bindEvents();
  loadTasks();
});

function bindEvents() {
  els.form.addEventListener("submit", onSubmit);
  els.cancelEdit.addEventListener("click", resetForm);

  [els.search, els.filterStatus, els.filterPriority, els.sort, els.order].forEach((el) => {
    el.addEventListener("change", loadTasks);
    if (el.id === "search") {
      el.addEventListener("input", debounce(loadTasks, 250));
    }
  });
}

async function onSubmit(event) {
  event.preventDefault();

  const payload = {
    title: els.title.value,
    description: els.description.value || null,
    priority: els.priority.value,
    status: els.status.value,
    dueDate: els.dueDate.value || null
  };

  const isEditing = Number.isInteger(state.editingId);
  const url = isEditing ? `/api/tasks/${state.editingId}` : "/api/tasks";
  const method = isEditing ? "PUT" : "POST";

  const response = await fetch(url, {
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  const data = await response.json();
  if (!response.ok || !data.ok) {
    showError(formatError(data));
    return;
  }

  resetForm();
  await loadTasks();
}

async function loadTasks() {
  hideError();
  const params = new URLSearchParams();
  if (els.search.value.trim()) params.set("search", els.search.value.trim());
  if (els.filterStatus.value) params.set("status", els.filterStatus.value);
  if (els.filterPriority.value) params.set("priority", els.filterPriority.value);
  params.set("sort", els.sort.value);
  params.set("order", els.order.value);

  const response = await fetch(`/api/tasks?${params.toString()}`);
  const payload = await response.json();

  if (!response.ok || !payload.ok) {
    showError(formatError(payload));
    return;
  }

  renderList(payload.data);
}

function renderList(tasks) {
  if (!tasks.length) {
    els.taskList.innerHTML = `<li class="collection-item">No tasks yet.</li>`;
    return;
  }

  els.taskList.innerHTML = tasks.map((task) => {
    const desc = task.description ? `<p>${escapeHtml(task.description)}</p>` : "";
    const due = task.dueDate ? `Due: ${task.dueDate}` : "No due date";
    return `
      <li class="collection-item">
        <div data-status="${task.status}">
          <strong>${escapeHtml(task.title)}</strong>
          <div class="task-meta">
            <span class="badge-status status-${task.status}">${task.status}</span>
            <span>${task.priority}</span>
            <span> - ${due}</span>
          </div>
          ${desc}
          <div class="secondary-content">
            <button class="btn-small" data-action="toggle" data-id="${task.id}">Toggle</button>
            <button class="btn-small blue" data-action="edit" data-id="${task.id}">Edit</button>
            <button class="btn-small red" data-action="delete" data-id="${task.id}">Delete</button>
          </div>
        </div>
      </li>`;
  }).join("");

  els.taskList.querySelectorAll("button[data-action]").forEach((button) => {
    button.addEventListener("click", onRowAction);
  });
}

async function onRowAction(event) {
  const id = Number.parseInt(event.currentTarget.dataset.id, 10);
  const action = event.currentTarget.dataset.action;

  if (action === "delete") {
    await fetch(`/api/tasks/${id}`, { method: "DELETE" });
    await loadTasks();
    return;
  }

  if (action === "toggle") {
    const currentStatus = event.currentTarget.closest("div[data-status]").dataset.status;
    await fetch(`/api/tasks/${id}/status`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ done: currentStatus !== "done" })
    });
    await loadTasks();
    return;
  }

  if (action === "edit") {
    const response = await fetch(`/api/tasks/${id}`);
    const payload = await response.json();
    if (!response.ok || !payload.ok) {
      showError(formatError(payload));
      return;
    }

    const task = payload.data;
    state.editingId = task.id;
    els.taskId.value = String(task.id);
    els.title.value = task.title;
    els.description.value = task.description ?? "";
    els.priority.value = task.priority;
    els.status.value = task.status;
    els.dueDate.value = task.dueDate ?? "";
    M.updateTextFields();
    M.FormSelect.init(document.querySelectorAll("select"));
    els.formTitle.textContent = `Edit task #${task.id}`;
  }
}

function resetForm() {
  state.editingId = null;
  els.form.reset();
  els.formTitle.textContent = "Create task";
  M.updateTextFields();
  M.FormSelect.init(document.querySelectorAll("select"));
}

function showError(message) {
  els.alert.textContent = message;
  els.alert.classList.remove("hide");
}

function hideError() {
  els.alert.classList.add("hide");
}

function formatError(payload) {
  if (!payload || !payload.error) {
    return "Unexpected error.";
  }

  const details = payload.error.details ?? [];
  return details.length ? `${payload.error.message} ${details.join(" ")}` : payload.error.message;
}

function escapeHtml(input) {
  return String(input)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function debounce(fn, ms) {
  let timer = null;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}
