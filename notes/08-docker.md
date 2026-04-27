## Docker

### Introduction

**What it is:**
Docker packages an application and everything it needs to run — runtime, libraries, system tools, configuration — into a single unit called a **container**. The container behaves the same on any machine that has Docker installed, regardless of the host operating system.

**Image vs container:**
- **Image** — a read-only template (a snapshot of a filesystem plus metadata). Think of it as a class.
- **Container** — a running instance of an image. Think of it as an object instantiated from that class.

You can start many containers from the same image; each one is independent.

**Why it matters:**
Without Docker, "it works on my machine" depends on installed runtimes, OS versions, environment variables, and a hundred other details. With Docker, the container carries its environment with it — the host only needs Docker itself.

---
### Downloading Docker Images

**What it is:**
Images live in a registry — by default, Docker Hub (`hub.docker.com`). The `docker pull` command downloads an image to your local machine so it can be used to start containers.

**How it fits:**
```
docker pull nginx
docker pull nginx:alpine
docker pull mcr.microsoft.com/dotnet/aspnet:9.0
```

`nginx` alone pulls the `latest` tag. `nginx:alpine` pulls the Alpine-based variant — a much smaller image (~40MB vs ~190MB) built on Alpine Linux instead of Debian.

You don't usually need to pull manually — `docker run` pulls the image automatically if it's not already on your machine.

---
### Running Docker Containers

**What it is:**
`docker run` creates a new container from an image and starts it.

**How it fits:**
```
docker run nginx
```

This starts an nginx container in the foreground. The terminal is attached to the container's logs, and `Ctrl+C` stops it.

**Common flags:**
- `-d` — detached: run in the background, return the terminal immediately
- `--name <name>` — give the container a human-readable name (otherwise Docker generates one like `quirky_einstein`)
- `--rm` — automatically remove the container when it stops (useful for one-off runs)

```
docker run -d --name nginx-test nginx
```

This starts nginx in the background, named `nginx-test`. Without a name, you'd have to look up the auto-generated one every time you want to reference it.

---
### Exposing Ports

**What it is:**
By default, a container is isolated from the host network — even if nginx is listening on port 80 inside the container, you can't reach it from your browser. The `-p` flag publishes a container port to a host port.

**How it fits:**
```
docker run -d --name nginx-test -p 8080:80 nginx
```

The format is `HOST:CONTAINER`. This maps host port `8080` to container port `80`. Visiting `http://localhost:8080` in the browser hits nginx inside the container.

**Why the two ports differ:**
The container's port (`80`) is fixed by the application — nginx listens on 80 by default. The host port (`8080`) is your choice. Multiple containers can each listen on their own internal port `80` as long as you map them to different host ports (`8080`, `8081`, etc.).

---
### Environment Variables

**What it is:**
Containers receive configuration through environment variables — the same mechanism the OS uses for `PATH`, `HOME`, etc. The `-e` flag sets one.

**How it fits:**
```
docker run -d --name nginx-test -e MY_VAR="Hello my guy" nginx
```

Inside the container, `echo $MY_VAR` prints `Hello my guy`.

**Why it's used:**
The same image can be configured per environment without rebuilding it — database connection strings, API keys, log levels, feature flags. The image stays generic; the variables make each running container specific.

---
### Volumes

**What it is:**
A container's filesystem is **ephemeral** — when the container is removed, everything written inside it is lost. A **volume** is storage managed by Docker that lives outside the container's lifecycle, so data persists across restarts and replacements.

**How it fits:**
```
docker run -d --name nginx-test -v nginx-data:/usr/share/nginx/html nginx
```

The format is `VOLUME-NAME:CONTAINER-PATH`. This mounts a Docker-managed volume named `nginx-data` at `/usr/share/nginx/html` inside the container — the directory nginx serves files from.

**Why it matters:**
- **Persistence** — delete and recreate the container; the volume's contents survive.
- **Sharing** — multiple containers can mount the same volume.
- **First-mount behavior** — if the volume is empty when first mounted, Docker copies the image's contents into it. So nginx's default welcome page still appears the first time.

---
### Entering a Running Container

**What it is:**
`docker exec` runs a command inside a container that's already running. Combined with an interactive shell, it gives you a terminal inside the container — useful for inspecting files, checking logs, or debugging.

**How it fits:**
```
docker exec -it nginx-test /bin/bash
```

**Breaking it down:**

`exec` — run a command in an existing container (different from `docker run`, which creates a new one).

`-i` (`--interactive`) — keeps STDIN open so you can type into the container.

`-t` (`--tty`) — allocates a pseudo-TTY, giving you a real terminal with a prompt, line editing, and colors.

`/bin/bash` — the program to run. The path is relative to the container's filesystem (Linux), not the host. Debian-based images like the default `nginx` have bash; Alpine-based images (`nginx:alpine`) only have `/bin/sh`.

To leave the shell without stopping the container, type `exit`.

---
### Docker Compose

**What it is:**
Long `docker run` commands with many flags become hard to remember and share. **Compose** lets you describe one or more containers in a YAML file and start them all with a single command.

**How it fits — `docker-compose.yml`:**
```yaml
services:
  nginx:
    image: nginx
    container_name: nginx-test
    ports:
      - "8080:80"
    volumes:
      - nginx-data:/usr/share/nginx/html
    environment:
      - MY_VAR=Hello my guy

volumes:
  nginx-data:
```

**Mapping to flags:**

| Compose key | Equivalent `docker run` flag |
|---|---|
| `image` | (positional argument) |
| `container_name` | `--name` |
| `ports` | `-p` |
| `volumes` | `-v` |
| `environment` | `-e` |

**Running it:**
```
docker compose up -d        # start everything in the background
docker compose down         # stop and remove the containers
docker compose logs -f      # follow the logs
```

**Watch out:** in the `environment` list form, quotes are **not stripped** — `MY_VAR="Hello"` sets the variable to the literal string `"Hello"` (with the quotes). Drop the quotes: `MY_VAR=Hello`.

---
### Nginx Example

**What it is:**
nginx is a high-performance web server. Its official Docker image serves files from `/usr/share/nginx/html` on port `80` by default — making it a useful concrete example for trying out images, ports, volumes, and exec.

**Putting it together:**
```
docker run -d \
  --name nginx-test \
  -p 8080:80 \
  -v nginx-data:/usr/share/nginx/html \
  -e MY_VAR="Hello my guy" \
  nginx
```

This single command:
- Pulls the `nginx` image if missing
- Starts a container named `nginx-test` in the background
- Maps host `8080` → container `80` (browse `http://localhost:8080`)
- Mounts a persistent volume at the web root
- Sets an environment variable inside the container

To get a shell inside it:
```
docker exec -it nginx-test /bin/bash
```

To replace your custom HTML, write files into the `nginx-data` volume — they appear instantly because nginx serves them on every request.

---
### Common Docker Commands

A reference of the commands you'll use most often:

| Command | What it does |
|---|---|
| `docker pull <image>` | Download an image from a registry |
| `docker images` | List images on your machine |
| `docker run <image>` | Create and start a new container |
| `docker ps` | List running containers |
| `docker ps -a` | List all containers (including stopped) |
| `docker stop <name>` | Gracefully stop a running container |
| `docker start <name>` | Start a stopped container |
| `docker restart <name>` | Stop and start a container |
| `docker rm <name>` | Remove a stopped container |
| `docker rmi <image>` | Remove an image |
| `docker logs <name>` | Print the container's stdout/stderr |
| `docker logs -f <name>` | Follow logs in real time |
| `docker exec -it <name> <cmd>` | Run a command inside a running container |
| `docker volume ls` | List volumes |
| `docker volume rm <name>` | Remove a volume |
| `docker compose up -d` | Start the stack defined in `docker-compose.yml` |
| `docker compose down` | Stop and remove the stack |

---
