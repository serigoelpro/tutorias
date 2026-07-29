# Plataforma de Tutorías — UTTN

## Importante: restauración de paquetes NuGet (LÉELO ANTES DE COMPILAR)

Las referencias del proyecto (`PlataformaWeb.csproj`) apuntan a los paquetes en
`..\packages\`, es decir un nivel ARRIBA de la raíz del repositorio, no dentro.

Esto significa que un `NuGet restore` estándar fallará al compilar, porque
restaura los paquetes en `./packages/` (junto al `.sln`) y el proyecto los busca
en `../packages/`. Aparecerían errores tipo "missing assembly reference" o de
Roslyn / CodeDom.

### Paso 1: Descargar nuget.exe

Descarga el ejecutable desde https://www.nuget.org/downloads (sección
"Windows x86 Commandline", versión recomendada / latest). Es un solo archivo,
no se instala. Guárdalo donde te sea cómodo, por ejemplo en tu carpeta de
Descargas.

### Paso 2: Restaurar a la ruta correcta

Desde PowerShell, ubícate en la raíz del repo (donde está `PlataformaWeb.sln`) y
ejecuta nuget.exe con su ruta completa, indicando la carpeta destino:

```
C:\Users\<usuario>\Downloads\nuget.exe restore PlataformaWeb.sln -PackagesDirectory ..\packages
```

Reemplaza `C:\Users\<usuario>\Downloads\` por la ruta donde dejaste nuget.exe.
Si la ruta tiene espacios, enciérrala en comillas y antepón `&`:

```
& "C:\ruta con espacios\nuget.exe" restore PlataformaWeb.sln -PackagesDirectory ..\packages
```

Al terminar, la carpeta `..\packages` debe quedar poblada con los paquetes. Luego
abre `PlataformaWeb.sln` en Visual Studio y compila (Ctrl+Shift+B).
