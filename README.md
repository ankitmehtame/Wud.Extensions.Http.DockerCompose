## Update docker-compose files for WUD based on trigger

Updates docker-compose files based on HTTP trigger configured in [WUD](https://github.com/fmartinou/whats-up-docker). The use-case is to update the docker-compose files with image tag automatically, otherwise the docker-compose file can be out of sync with what's running in docker (except for stable/latest or fixed version cases).

### Inputs
  - Docker Compose file paths should be provided in a comme separated environment variable `DOCKER_FILES_CSV` to this container. Remember to also pass through the files to the container in a volume.
  - Environment variable `WUD_CONTAINERS_URL` should be populated with your What's Up Docker containers API URL, which is typically instance URL appended with `/api/containers`

Docker Compose example:
```
---
version: '3'

services:
  wud-http-extension:
    container_name: wud-http-extension
    image: ankitmehtame/wud-extensions-http-docker-compose
    volumes:
      - /docker-data/homeassistant/docker-compose.yml:/files/homeassistant/docker-compose.yml:rw
    environment:
      - DOCKER_FILES_CSV=/files/homeassistant/docker-compose.yml
      - WUD_CONTAINERS_URL=http://192.168.1.5:3025/api/containers
    ports:
      - 15027:8080 # Your preferred host port mapped to internal 8080
    restart: unless-stopped
```

Finally, add a trigger for your WUD instance (https://fmartinou.github.io/whats-up-docker/#/configuration/triggers/http/) and point it to this container's URL.
```
  # In WUD container
  environment:
    - WUD_TRIGGER_HTTP_ALL_URL=http://192.168.1.5:15027/api/container-new-version
```
Substitute `http://192.168.1.5:15027` with your container's URL and port.
