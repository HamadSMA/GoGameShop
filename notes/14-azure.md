## Azure

### Introduction to the Cloud

**The problem:**
Running an application traditionally meant owning the box it ran on. You bought servers, racked them in a room, paid for power and cooling, hired people to swap failing disks at 3am, and over-provisioned everything for a traffic spike that might never come. Scaling up took weeks of procurement; scaling down meant hardware sitting idle. Disaster recovery was a second data centre nobody could afford. The whole model coupled the business to physical infrastructure it did not want to be in the business of running.

**What it does:**
The "cloud" is someone else's computers, rented on demand over the internet, with the boring parts (hardware, power, networking, redundancy) abstracted away. You ask for a server, a database, a queue, or a function, and a provider like Azure, AWS, or GCP gives you one in seconds, bills you per hour or per request, and lets you give it back just as fast. The same compute that would have taken three months to procure now takes thirty seconds in a portal.

**What makes a server "cloud" and not just a remote server:**
A box in a colocation rack is just a remote server. For the label "cloud" to apply, five conditions (NIST's definition) need to hold:
1. **On-demand self-service** : you provision resources yourself through a portal, CLI, or API, without filing a ticket with a human
2. **Broad network access** : reachable over standard internet protocols from any client (browser, phone, CI runner)
3. **Resource pooling** : the provider's hardware is shared across many tenants; you do not know or care which physical machine you are on
4. **Rapid elasticity** : you can scale up and down quickly, often automatically, and it feels limitless from your side
5. **Measured service** : usage is metered (CPU-hours, GB-months, requests) and you pay only for what you use

A single dedicated server you SSH into fails most of these. A managed App Service that autoscales and bills per minute satisfies all of them.

---
### Cloud vs Local Servers

**The problem:**
Deciding whether to host on the cloud or on your own hardware is not a fashion choice; the two models have genuinely different cost curves, control levels, and operational burdens. Picking the wrong one wastes money or saddles a small team with infrastructure work that has nothing to do with the product.

**What it does:**
The trade-off comes down to who owns the layers underneath your code and who pays for idle capacity.

| Aspect              | Local servers (on-premises)                      | Cloud                                                     |
|---------------------|--------------------------------------------------|-----------------------------------------------------------|
| **Up-front cost**   | High (buy hardware, build the room)              | None (pay as you go)                                      |
| **Scaling**         | Buy more hardware; weeks to months               | Click or API call; seconds to minutes                     |
| **Idle capacity**   | You pay for it whether used or not               | Scale down, stop paying                                   |
| **Maintenance**     | Your team patches, replaces, monitors hardware   | Provider handles the physical and platform layers         |
| **Control**         | Full: every layer down to the BIOS               | Limited: you control what the chosen service exposes      |
| **Compliance**      | You can keep data physically on-premises         | Depends on the region and certifications the provider has |
| **Failure domain**  | Your data centre, your power, your internet      | Provider regions and zones (still possible to lose one)   |
| **Time to provision** | Procurement cycle                              | Seconds                                                   |

**Problems the cloud came to solve:**
- **Capital expense locking up cash** : turn a large up-front purchase into a metered operating expense
- **Over-provisioning for peak load** : pay for peak only when peak happens
- **Slow provisioning** : remove the procurement cycle from the critical path of shipping
- **Geographic reach** : deploy to regions on other continents without owning data centres there
- **Undifferentiated heavy lifting** : let a provider operate the hardware, hypervisor, and platform so your team can focus on the application

For a learning project like this one, the relevant point is mostly the last two: Azure lets a single developer deploy a real, internet-reachable .NET API plus a database plus a frontend without owning or operating any of the infrastructure underneath.

---
### Cloud and Azure Basics

**The problem:**
"The cloud" is a marketing umbrella over a hundred different services with overlapping names, and Azure in particular has a reputation for sprawling terminology. Before touching any of it, a few core ideas need to be straight or every later concept lands on quicksand.

**What it does:**
The minimum mental model to carry into the rest of these notes:

- **Region** : a geographic location (e.g. `westeurope`, `eastus`) where Azure has one or more data centres. You pick a region for each resource. Closer regions mean lower latency to your users; some services are region-specific.
- **Availability zone** : physically separate data centres inside a single region with independent power and networking, so one zone failing does not take the others down. Used by services that need high availability.
- **Resource** : a single thing you create in Azure : a virtual machine, a database, a storage account, an App Service. Every resource has a globally unique ID and lives in exactly one resource group.
- **Resource group** : a logical container for related resources. Convention is one resource group per application or environment (e.g. `gogameshop-dev-rg`). Deleting a resource group deletes everything inside it, which makes it the natural unit for cleanup.
- **Subscription** : the billing boundary. Every resource group lives inside a subscription, and every charge is rolled up there. An organization typically has several (dev, test, prod).
- **Tenant (Microsoft Entra ID directory)** : the identity boundary. Users, groups, and service principals live in a tenant; subscriptions trust a tenant for authentication.
- **ARM (Azure Resource Manager)** : the control plane. Every create / read / update / delete against an Azure resource, whether from the portal, the CLI, Terraform, or a SDK, goes through ARM. This is why permissions and tagging work uniformly across services.

These six terms cover ~90% of the conversations you will have with Azure documentation.

---
### Shared Responsibility Model

**The problem:**
When the provider runs the hardware and you run the application, it stops being obvious who is responsible for what. Is patching the OS the provider's job or yours? What about backing up data, encrypting it, configuring the firewall? Getting this wrong is how cloud breaches happen : everyone assumes someone else had it covered.

**What it does:**
The **shared responsibility model** is the explicit split of duties between the cloud provider and the customer. The boundary shifts depending on which service model you use (IaaS / PaaS / SaaS), but the layers are always the same:

| Layer              | What it is                                                                |
|--------------------|---------------------------------------------------------------------------|
| **Data**           | The bytes you store: rows in a DB, files in blob storage, user uploads    |
| **Application**    | Your code: the API, the frontend, business logic                          |
| **Runtime**        | The language runtime executing your code: .NET, Node, JVM                 |
| **Middleware**     | Shared services your app depends on: web servers, message brokers, caches |
| **Operating system** | Linux or Windows running underneath everything                          |
| **Virtualization** | The hypervisor that slices physical machines into VMs                     |
| **Servers**        | The physical compute hardware                                             |
| **Storage**        | The physical disks and storage arrays                                     |
| **Networking**     | Switches, routers, cables, the physical network                           |

The provider always owns the bottom four (servers, storage, networking, virtualization), no matter what. Customer-owned layers move up the stack as the service model becomes more managed.

Two responsibilities the customer **always** keeps, regardless of service model:
- **Identity and access** : who can log in, what they can do
- **Data classification and content** : what you put into the service, and whether it should be there

The provider can encrypt your storage, but they cannot stop you from uploading a plaintext password file.

---
### Cloud Service Models : IaaS, PaaS, SaaS

**The problem:**
Different applications want different amounts of control. A team running a legacy Windows app with custom drivers needs the OS exposed. A team shipping a stateless web API does not want to think about the OS at all. A team that just needs email does not even want to think about the application. One-size-fits-all does not work, so cloud providers offer three tiers.

**What it does:**
The three classic service models trade control for managed convenience. Each one moves the customer / provider line further up the stack in the shared responsibility model.

| Layer            | IaaS (e.g. Azure VM) | PaaS (e.g. App Service) | SaaS (e.g. Microsoft 365) |
|------------------|----------------------|-------------------------|---------------------------|
| Data             | You                  | You                     | You                       |
| Application      | You                  | You                     | Provider                  |
| Runtime          | You                  | Provider                | Provider                  |
| Middleware       | You                  | Provider                | Provider                  |
| Operating system | You                  | Provider                | Provider                  |
| Virtualization   | Provider             | Provider                | Provider                  |
| Servers          | Provider             | Provider                | Provider                  |
| Storage          | Provider             | Provider                | Provider                  |
| Networking       | Provider             | Provider                | Provider                  |

**IaaS (Infrastructure as a Service):**
The provider gives you a virtual machine and a network; you bring everything from the OS up. Maximum control, maximum responsibility. Good for legacy software that needs OS-level access, custom kernel modules, or licensing tied to specific hardware. Example: Azure Virtual Machines.

**PaaS (Platform as a Service):**
The provider gives you a managed runtime; you just deploy your application code. They patch the OS, run the web server, scale the instances, handle TLS certificates. You give up the ability to SSH into the box but never need to. Good for standard web apps, APIs, and databases. Example: Azure App Service, Azure SQL Database.

**SaaS (Software as a Service):**
The provider gives you a finished application; you just log in and use it. No deployment, no code, no infrastructure decisions. You only own your data and your users. Example: Microsoft 365, GitHub, Keycloak Cloud.

**In this project:**
GoGameShop will be hosted on **PaaS**. The API and frontend run on Azure App Service (and later Container Apps), the database runs on Azure SQL Database, and storage uses Azure Blob Storage. Azure handles the OS, the runtime patching, the web server, the disk failures; this project's responsibility ends at the C# code, the EF Core migrations, and the appsettings configuration. IaaS would mean spinning up a VM and installing .NET ourselves: more control, but no upside for a project that has no OS-level needs.

---
### Azure Physical and Management Infrastructure

**The problem:**
A single subscription works fine for a hobby project, but real organizations have dev environments, test environments, production environments, multiple teams, multiple cost centres, and compliance rules that differ per environment. Flat structure stops working: you need a way to apply policies, budgets, and access controls to groups of subscriptions at once.

**What it does:**
Azure organizes resources into a four-level hierarchy. Each level is a scope at which permissions, policies, and budgets can be applied, and settings flow downward (a policy set at the top applies to everything underneath).

```
Management Group
        |
        +-- Dev Subscription
        |        |
        |        +-- Resource Group
        |                 |
        |                 +-- Resource (App Service, SQL DB, Storage Account)
        |
        +-- Test Subscription
        |        |
        |        +-- Resource Group
        |                 |
        |                 +-- Resource
        |
        +-- Production Subscription
                 |
                 +-- Resource Group
                          |
                          +-- Resource
```

- **Management group** : the top-level grouping. Used to apply a policy ("no resources outside Europe") or an RBAC role ("the security team is Reader on everything") across many subscriptions at once. Management groups can be nested.
- **Subscription** : the billing and quota boundary. Each subscription has its own invoice, its own quotas, and its own set of resources. The dev / test / production split lives here because it cleanly separates blast radius: a mistake in dev cannot accidentally touch production.
- **Resource group** : a logical container for the resources that belong to a single application or workload. Convention: `gogameshop-dev-rg`, `gogameshop-prod-rg`. Deleting the group deletes everything inside, which makes teardown a one-liner.
- **Resource** : the actual thing : an App Service, a SQL Database, a Storage Account.

Underneath all of this is the **physical infrastructure** : regions (geographic locations), availability zones (separate data centres inside a region), and the data centres themselves. When you pick a region for a resource group, every resource in it lands in that region's physical infrastructure.

**In this project:**
For a single-developer learning project, the hierarchy collapses to one subscription and one or two resource groups (e.g. `gogameshop-dev-rg`). Management groups only earn their keep when there are multiple subscriptions to govern, but it is worth knowing the model so the portal's left-hand nav makes sense.

---
### Azure Role-Based Access Control (RBAC)

**The problem:**
"Admin or nothing" does not work in a real cloud account. The DBA needs to manage databases but not delete the network. The CI service principal needs to deploy the API but not read production secrets. The intern needs read-only access to staging. Without fine-grained access control, the only safe option is to give everyone admin, which is the same as giving everyone the ability to take production down by accident.

**What it does:**
**Azure RBAC** controls who can do what on which resources. Every permission check answers three questions:

1. **Who** : the security principal : a user, a group, a service principal (app), or a managed identity
2. **What** : the role : a named bundle of permitted actions (e.g. `Reader`, `Contributor`, `Owner`, `Storage Blob Data Reader`)
3. **Where** : the scope : the level in the hierarchy where the assignment applies : management group, subscription, resource group, or a single resource

A **role assignment** is the tuple `(principal, role, scope)`. Assigning `Contributor` on `gogameshop-dev-rg` to a user lets them create and modify resources in that resource group but nowhere else.

**Built-in roles worth knowing:**
- **Owner** : full access plus the ability to delegate access to others
- **Contributor** : full access to manage resources, but cannot grant access to others
- **Reader** : view everything, change nothing
- **User Access Administrator** : manage access to resources, but cannot manage the resources themselves
- **Service-specific data roles** : e.g. `Storage Blob Data Reader`, `Key Vault Secrets User`. These are needed for the **data plane** (reading the bytes inside a blob, the value of a secret) as distinct from the **control plane** (creating or deleting the resource itself). A user with `Contributor` on a storage account can rename the account but still needs `Storage Blob Data Reader` to actually read blob contents.

Permissions are **additive**: if you have `Reader` at the subscription scope and `Contributor` at a resource group inside it, you are a Contributor in that group. There is no "deny" in standard RBAC (deny assignments exist but are rare and managed by Azure itself for things like Azure Blueprints).

**In this project:**
At first there is only one user (you) with `Owner` on the subscription, and RBAC barely shows up. It becomes relevant when:
- A GitHub Actions workflow needs a service principal scoped narrowly to the resource group it deploys to
- The App Service needs a **managed identity** with `Key Vault Secrets User` so it can read connection strings without storing them in config

---
### Hosting Options and Trade-offs

**The problem:**
Even after committing to "PaaS on Azure", there are five different places you could run a .NET API, and the right choice depends on how the app is packaged, how often it runs, and how much operational complexity you want to take on. Picking the wrong one means either fighting the platform or paying for capabilities you do not use.

**What it does:**
The five mainstream Azure compute options, from most control to most managed:

| Option                          | What it is                                                                                                   | Good for                                                                  | Trade-off                                                                  |
|---------------------------------|--------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------|----------------------------------------------------------------------------|
| **Virtual Machines (VMs)**      | IaaS: a Windows or Linux VM you SSH into                                                                     | Legacy apps, custom kernel/drivers, software with strict OS dependencies  | You patch the OS, configure the web server, manage scaling                 |
| **App Service**                 | PaaS: deploy a zip, a container, or wire up Git; Azure runs it on a managed Linux or Windows host            | Standard web apps and APIs with predictable traffic                       | Less customization than a VM; pricing tiers gate features (slots, scaling) |
| **Azure Kubernetes Service (AKS)** | Managed Kubernetes: Azure runs the control plane, you run the workloads                                   | Microservice fleets, teams that already know Kubernetes                   | Kubernetes is its own steep operational burden                             |
| **Container Apps**              | Managed serverless containers built on Kubernetes + KEDA, but without exposing Kubernetes itself             | Containerized apps that need autoscaling (including scale-to-zero), background jobs, microservices without running K8s | Less control than AKS; some features (DaemonSets, custom networking) absent |
| **Functions**                   | Serverless: write a single function, Azure invokes it on a trigger (HTTP, queue, timer) and bills per execution | Event-driven workloads, glue code, low-volume APIs                       | Cold starts; not ideal for long-running or stateful work                   |

**Rules of thumb:**
- If the app is a normal HTTP API and you want the least friction, start with **App Service**.
- If the app ships as a container and benefits from scale-to-zero, prefer **Container Apps**.
- If you need to run Kubernetes itself (or your team already does), **AKS**.
- If the work is event-driven and bursty, **Functions**.
- If you genuinely need OS access, **VMs**, but only then.

**In this project:**
GoGameShop will run on **App Service** first. The API is a standard ASP.NET Core app, traffic is low and predictable, and App Service is the shortest path from `dotnet publish` to a public URL with TLS. Later it will move to **Container Apps**, after the API is containerized: the same app, but with scale-to-zero (no idle cost when no one is shopping) and better fit for the container-first workflow established in [Docker](09-docker.md).

---
### Homebrew

**The problem:**
Installing developer tools on macOS by downloading individual `.pkg` files or building from source is slow, inconsistent, and leaves no audit trail of what is installed. Updates require re-downloading; uninstalls leave files behind; there is no shared command anyone else can run to get the same setup.

**What it does:**
**Homebrew** is the de facto package manager for macOS (and Linux). It installs command-line tools and applications into a sandbox under `/opt/homebrew` (Apple Silicon) or `/usr/local` (Intel) so they do not collide with system files, and it tracks every install for clean updates and removals.

Two kinds of packages:
- **Formulae** : command-line tools (e.g. `azure-cli`, `git`, `dotnet`)
- **Casks** : GUI applications packaged as `.app` bundles (e.g. `microsoft-azure-storage-explorer`, `docker`)

**Common commands:**
```
brew install <name>          # install a formula
brew install --cask <name>   # install a cask (GUI app)
brew upgrade                 # upgrade everything
brew uninstall <name>        # remove a package
brew list                    # see what's installed
brew search <term>           # find a package
```

**In this project:**
Homebrew is how the Azure CLI and Azure Storage Explorer get installed on macOS without dealing with Microsoft's installer downloads. Future tooling (the Bicep CLI, `azcopy`, etc.) installs the same way.

---
### Azure CLI

**The problem:**
Clicking through the Azure portal is fine for exploring, but anything that needs to be reproducible (deploys, environment setup, CI/CD) cannot be a sequence of mouse clicks. There has to be a scriptable interface to ARM so that "create the resource group" or "deploy the API" is a command, not a screenshot in a runbook.

**What it does:**
The **Azure CLI** (`az`) is the cross-platform command-line tool for Azure. Every action the portal can take is also an `az` subcommand, because both go through the same ARM API underneath. The CLI authenticates once (`az login` opens a browser), then commands run against the logged-in identity.

**Install on macOS:**
```
brew install azure-cli
```

**Basic commands:**
```
az login                                  # browser-based login
az account show                           # show the current subscription
az account list --output table            # list all subscriptions you can see
az account set --subscription <id-or-name># switch active subscription
az group create -n gogameshop-dev-rg -l westeurope  # create a resource group
az group list --output table              # list resource groups
az webapp list --output table             # list App Services in the current subscription
```

**Output formats:**
The `--output` (or `-o`) flag controls how results are printed. `table` is human-readable, `json` is the default and is what you pipe to `jq` for scripting, `tsv` is useful for assigning a single value to a shell variable.

```
RG_ID=$(az group show -n gogameshop-dev-rg --query id -o tsv)
```

The `--query` flag uses [JMESPath](https://jmespath.org) to drill into the JSON before output, so the CLI doubles as a structured query tool against Azure resources.

**In this project:**
The CLI is the primary way to create the resource group, the App Service plan, the Web App, the SQL Database, and the Storage Account during Phase 5. Once the names and SKUs are right in shell commands, those same commands become the basis for the GitHub Actions deployment workflow.

---
### Azure Storage Explorer

**The problem:**
Azure Storage Accounts hold blobs (files), queues, tables, and file shares. The portal can browse them, but it is slow, refreshes oddly, and is awful for bulk operations like uploading a directory of seed images or sanity-checking what the API actually wrote. Doing this with `az storage blob` works but is verbose for visual inspection.

**What it does:**
**Azure Storage Explorer** is a free desktop application (Windows, macOS, Linux) for browsing and managing Azure Storage accounts and related services (Cosmos DB, Data Lake). It signs into Azure with the same identity used by `az login`, lists every storage account that identity can see, and exposes blob containers as a familiar tree view : drag and drop to upload, double-click to download, right-click for shared access signatures.

**Install on macOS:**
```
brew install --cask microsoft-azure-storage-explorer
```

**What it is good for:**
- Uploading seed assets (game cover images for this project) without writing a script
- Inspecting whether the API actually wrote a blob to the container it claims
- Generating a SAS URL for sharing a single blob with a teammate without granting them subscription access
- Browsing emulated storage when running **Azurite** (the local storage emulator) during development

**In this project:**
When the games catalog moves from `wwwroot` to Azure Blob Storage, Storage Explorer is the GUI for confirming uploads happened, fixing content-type metadata, and pulling blobs back down to verify them. The Azure CLI can do the same things, but Storage Explorer is faster for the "did the file land where I expected" feedback loop.
