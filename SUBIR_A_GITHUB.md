# Subir AirSolutions a GitHub

## 1. Crear el repositorio en GitHub

- Se abrió una pestaña en el navegador con **github.com/new** y el nombre **AirSolutions**.
- Si no la ves, entra en: https://github.com/new?name=AirSolutions
- **No marques** "Add a README file" (el proyecto ya tiene uno).
- Pulsa **Create repository**.

## 2. Poner tu usuario de GitHub en el remoto

Si tu usuario de GitHub **no** es `crist`, ejecuta en la terminal (sustituye `TU_USUARIO` por tu usuario real):

```powershell
cd "c:\Users\crist\IA Projects"
git remote set-url origin https://github.com/TU_USUARIO/AirSolutions.git
```

## 3. Subir el proyecto

En la misma carpeta:

```powershell
git push -u origin master
```

Si GitHub te pide iniciar sesión, hazlo en el navegador o con el método que uses (token, SSH, etc.).

---

**Resumen:** Crear el repo vacío en GitHub → (opcional) corregir la URL del remote → `git push -u origin master`.
