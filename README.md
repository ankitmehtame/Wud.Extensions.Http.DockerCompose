## Update docker-compose files for WUD based on trigger

Updates docker-compose files based on HTTP trigger configured in ![WUD](https://github.com/fmartinou/whats-up-docker). Docker Compose file paths should be provided in a comme separated environment variable `DOCKER_FILES_CSV` to this container.
The use-case is to update the docker-compose files with image tag automatically, otherwise the docker-compose file can be out of sync with what's running in docker (except for stable/latest or fixed version cases).
