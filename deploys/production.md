# Backend
### Build
docker build -t ghcr.io/hung6066/ivf:manual -f src/IVF.API/Dockerfile .

### Transfer & load on VPS
docker save ghcr.io/hung6066/ivf:manual | ssh root@45.134.226.56 "docker load"

### Rolling update
ssh root@45.134.226.56 "docker service update --image ghcr.io/hung6066/ivf:manual --update-order start-first --force ivf_api"

# Frontend
### Build
docker build -t ghcr.io/hung6066/ivf-client:manual -f ivf-client/Dockerfile .

### Transfer & load on VPS
docker save ghcr.io/hung6066/ivf-client:manual | ssh root@45.134.226.56 "docker load"

### Update
ssh root@45.134.226.56 "docker service update --image ghcr.io/hung6066/ivf-client:manual --update-order start-first --force ivf_frontend"

# Verify
ssh root@45.134.226.56 "curl -sk https://natra.site/health/live && echo && docker service ls --filter name=ivf_api --filter name=ivf_frontend --format 'table {{.Name}}\t{{.Replicas}}\t{{.Image}}'"


# Fast
### Backend
docker build -t ghcr.io/hung6066/ivf:lynis1 -f src/IVF.API/Dockerfile . && docker save ghcr.io/hung6066/ivf:lynis1 | ssh root@10.200.0.1 "docker load" && ssh root@10.200.0.1 "docker service update --image ghcr.io/hung6066/ivf:lynis1 --update-order start-first --force ivf_api"

### Frontend
docker build -t ghcr.io/hung6066/ivf-client:lynis1 -f ivf-client/Dockerfile . && docker save ghcr.io/hung6066/ivf-client:lynis1 | ssh root@10.200.0.1 "docker load" && ssh root@10.200.0.1 "docker service update --image ghcr.io/hung6066/ivf-client:lynis1 --update-order start-first --force ivf_frontend"