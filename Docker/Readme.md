# Sample cluster build with docker-compose

Please refer the [build scripts documentation](../BuildScript.md) for easy images build.

## Docker-compose

**KlusterKite** folder - contains the `docker-compose` description of sample cluster. After all docker images are built the sample cluster can be started as `docker-compose up -d` command from this directory.

The `docker-compose` will start the following services:
* Vpn-server to let host system attach to the containers network
* Nuget private server
* PostgreSQL to store `KlusterKite.Nodemanager` configuration database
* Redis server to store authentication tokens
* ELK stack server to store and analyze logs
* Seed service to provide Akka.NET cluster lighthouse service
* Nginx entry point to publish all web-based service outside the cluster
* publisher1 and publisher2 services to demonstrate redundant cluster internal web publishing (these containers are made from `klusterkite/publisher` image, see below)
* monitoringUI to publish `KlusterKite.Nodemanager` UI
* seeder service to initialize the configuration database on cluster first start
* manager - the service from `klusterkite/manager` docker image (see below). This type of service can be safely scaled to add more nodes to the cluster
* manager - the service from `klusterkite/worker` docker image (see below). This type of service can be safely scaled to add more nodes to the cluster


## Base images

This is the list of docker images that are either used as base images for som other or just contains third-party services.

* **KlusterKiteBaseWorkerNode** docker image with Ubuntu system and CoreCLR installed. This is built to `klusterkite/baseworker` docker image with build scripts. It's used as a base for other images.
* **KlusterKiteBaseWebNode** the same as **KlusterKiteBaseWorkerNode**, but with additional Nginx installed. This is built to `klusterkite/baseweb` docker image with build scripts. It's used as a base for other images.
* **KlusterKiteNuget** - the simple private NuGet server based on [Simple NuGet Server](https://github.com/Daniel15/simple-nuget-server/) project. This is built to `klusterkite/nuget` docker image with build scripts. 
* **KlusterKitePostgres** - the ubuntu server with Postgres installed. This is built to `klusterkite/Postgres` docker image with build scripts. 
* **KlusterKiteVpn** - the OpenVPN server to get easy access to docker containers network with direct network access to the containers. The OpenVPN client configuration is in the `Client` folder.
* **KlusterKiteELK** - the ElasticSearch and Kibana. This is built to `klusterkite/elk` docker image with build scripts. 
* **KlusterKite.Redis** - just the Redis server. This is built to `klusterkite/redis` docker image with build scripts. 
* **KlusterKiteEntry** - pre-configured Nginx server to act as system entry point. Please check the [`KlusterKite.Web`](../KlusterKite.Web/Readme.md) documentation.

## The worker images

This is the list of docker images that contains the code to launch Akka.NET cluster nodes.

* **KlusterKiteSeed** - the Akka.NET Cluster lighthouse. This is built to `klusterkite/seed` docker image with build scripts.
* **KlusterKiteSeeder** - the one-time run script to perform database seeding. This is built to `klusterkite/seeder` docker image with build scripts.
* **KlusterKiteManager** - the image preconfigured to hold the `KlusterKite.NodeManager` and `KlusterKite.Monitoring` functions. Contains the `KlusterKite.NodeManager.Launcher` that is configured with `manager` container type and have an appropriate fallback configuration for the cluster start procedure. This is built to `klusterkite/manager` docker image with build scripts.
* **KlusterKitePublisher** - the image preconfigured to hold the `KlusterKite.Web.NginxConfigurator` functions. Contains the `KlusterKite.NodeManager.Launcher` that is configured with `publisher` container type and have an appropriate fallback configuration for the cluster start procedure. Also, have installed Nginx in it. This is built to `klusterkite/publisher` docker image with build scripts.
* **KlusterKiteWorker** - the image has no preconfigured functions and will wait for **system** to assign them. Contains the `KlusterKite.NodeManager.Launcher` that is configured with `worker` container type and doesn't have any fallback configuration, so it will not start until the cluster will assign a function to it. This is built to `klusterkite/worker` docker image with build scripts.
* **KlusterKiteMonitoring** - contains a static Web-site with **KlusterKite** management UI based on React/Relay. This is built to `klusterkite/monitoring-ui` docker image with build scripts.
