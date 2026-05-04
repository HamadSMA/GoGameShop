## Docker

### Introduction

**The problem:**
"It works on my machine" depends on installed runtimes, OS versions, environment variables, and a hundred other details that drift between developer laptops, CI runners, and production servers. Without a way to freeze that environment, every deploy is a gamble and every onboarding doc is a 30-step trap.

**What it does:**
Docker packages an application and everything it needs to run — runtime, libraries, system tools, configuration — into a single unit called a **container**. The container behaves the same on any machine that has Docker installed, regardless of the host operating system.

**Image vs container:**
- **Image** — a read-only template (a snapshot of a filesystem plus metadata). Think of it as a class.
- **Container** — a running instance of an image. Think of it as an object instantiated from that class.

You can start many containers from the same image; each one is independent.

---
### Downloading Docker Images

**The problem:**
An image has to exist locally before you can run a container from it. Building everything from scratch each time would be slow; somewhere has to be a shared store of pre-built images you can fetch.

**What it does:**
Images live in a **registry** — by default, Docker Hub (`hub.docker.com`). The `docker pull` command downloads an image to your local machine so it can be used to start containers. (You don't usually need to pull manually — `docker run` pulls automatically if the image isn't already present.)

**In code:**
```
docker pull nginx
docker pull nginx:alpine
docker pull mcr.microsoft.com/dotnet/aspnet:9.0
```

`nginx` alone pulls the `latest` tag. `nginx:alpine` pulls the Alpine-based variant — a much smaller image (~40MB vs ~190MB) built on Alpine Linux instead of Debian.

---
### Running Docker Containers

**The problem:**
An image is just a frozen filesystem; nothing happens until you turn it into a live process. You need a way to instantiate a container, control whether it stays in the foreground, and refer back to it later by name.

**What it does:**
`docker run` creates a new container from an image and starts it. Flags control its lifecycle (`-d` to detach, `--rm` for one-off cleanup) and its identity (`--name` for a stable handle instead of a random one like `quirky_einstein`).

**In code:**
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

**The problem:**
Containers are network-isolated by default. Even if a service inside is listening on port 80, the host can't reach it — and you can't run two containers that both want port 80 if they share the host network namespace.

**What it does:**
The `-p` flag publishes a container port to a host port, mapping `HOST:CONTAINER`. The container's port stays whatever the application uses; the host port is your choice, so multiple containers can each listen on their own internal `80` and be reached on different host ports (`8080`, `8081`, …).

**In code:**
```
docker run -d --name nginx-test -p 8080:80 nginx
```

This maps host port `8080` to container port `80`. Visiting `http://localhost:8080` in the browser hits nginx inside the container.

---
### Environment Variables

**The problem:**
The same image needs to behave differently in dev, staging, and production — different database URLs, different API keys, different log levels. Baking those into the image means rebuilding per environment and risking secrets in committed layers.

**What it does:**
Containers receive configuration through environment variables — the same mechanism the OS uses for `PATH`, `HOME`, etc. The `-e` flag sets one at run time, so the image stays generic and each running container is parameterized at startup.

**In code:**
```
docker run -d --name nginx-test -e MY_VAR="Hello my guy" nginx
```

Inside the container, `echo $MY_VAR` prints `Hello my guy`.

---
### Volumes

**The problem:**
A container's filesystem is **ephemeral** — when the container is removed, everything written inside it is lost. That's fine for stateless services, but a database, a Keycloak realm, or uploaded user files would vanish on every redeploy.

**What it does:**
A **volume** is storage managed by Docker that lives outside the container's lifecycle. Mount it at a path inside the container and writes go to durable host storage instead of the throwaway container layer — so data survives restarts and replacements, and multiple containers can share the same volume.

**In code:**
```
docker run -d --name nginx-test -v nginx-data:/usr/share/nginx/html nginx
```

The format is `VOLUME-NAME:CONTAINER-PATH`. This mounts a Docker-managed volume named `nginx-data` at `/usr/share/nginx/html` inside the container — the directory nginx serves files from.

**Behaviors worth knowing:**
- **Persistence** — delete and recreate the container; the volume's contents survive.
- **Sharing** — multiple containers can mount the same volume.
- **First-mount behavior** — if the volume is empty when first mounted, Docker copies the image's contents into it. So nginx's default welcome page still appears the first time.

---
### Entering a Running Container

**The problem:**
A container is a running process with its own filesystem and process tree, but you can't `cd` into it from the host or attach a debugger directly. You need a way to step inside and poke at it — list files, tail logs, check why something isn't responding.

**What it does:**
`docker exec` runs a command inside a container that's already running. Combined with an interactive shell, it effectively gives you a terminal session inside the container's environment without restarting it.

**In code:**
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

**The problem:**
Long `docker run` commands with many flags become hard to remember, share, and version-control. Real apps usually need *several* containers (API + database + cache) wired together — orchestrating that by hand is fragile and impossible to onboard a teammate to.

**What it does:**
**Compose** describes one or more containers in a YAML file and starts them all with a single command. The file is a versionable, shareable artifact — `docker compose up` recreates the entire stack identically on any machine.

**In code — `docker-compose.yml`:**
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

**Why this one:**
nginx is a high-performance web server. Its official Docker image serves files from `/usr/share/nginx/html` on port `80` by default — small, well-known, and exercises every concept above (image, port, volume, env var, exec) in one command.

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
