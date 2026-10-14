# Local Docker stack

The stack runs two containers:

- `api`: the WorldSeed local persistence API on `http://localhost:5080`.
- `mongo`: the private local MongoDB database used by the API.

The native Avalonia desktop application remains on the host computer. Local Ollama, model files, and source files are never copied into or mounted by Docker. A source file is read only when the designer selects it in the desktop application; its imported text becomes part of that project's database record.

## First start

1. Install and start Docker Desktop.
2. Run `deploy\worldseed-local.bat`.
3. The script copies `.env.example` to the ignored local file `deploy\.env` and builds/starts the stack.
4. Confirm `http://localhost:5080/health` reports `healthy`.

Use `worldseed-local.bat down` to stop containers without deleting data. Use `worldseed-local.bat reset` for a deliberately destructive fresh install: it removes the Mongo named volume, then rebuilds and starts the stack.

Do not commit `deploy\.env`; it is machine-specific and contains the local database password.
