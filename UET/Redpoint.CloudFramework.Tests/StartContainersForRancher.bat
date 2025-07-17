net stop winnat

rdctl shell curl http://192.168.127.1:80/services/forwarder/unexpose -X POST -d "{\"local\":\":61000\",\"remote\":\"192.168.127.2:61000\"}"
rdctl shell curl http://192.168.127.1:80/services/forwarder/unexpose -X POST -d "{\"local\":\":61001\",\"remote\":\"192.168.127.2:61001\"}"
rdctl shell curl http://192.168.127.1:80/services/forwarder/unexpose -X POST -d "{\"local\":\":61002\",\"remote\":\"192.168.127.2:61002\"}"

docker stop rcftest-datastore
docker stop rcftest-pubsub
docker stop rcftest-redis

docker run --rm -d --name rcftest-datastore -p 61002:9001 ghcr.io/redpointgames/uet/firestore-in-datastore-mode-emulator:latest
docker run --rm -d --name rcftest-pubsub -p 61001:9000 ghcr.io/redpointgames/uet/pubsub-emulator:latest
docker run --rm -d --name rcftest-redis -p 61000:6379 redis:6.0.10

net start winnat

rdctl shell curl http://192.168.127.1:80/services/forwarder/expose -X POST -d "{\"local\":\":61000\",\"remote\":\"192.168.127.2:61000\"}"
rdctl shell curl http://192.168.127.1:80/services/forwarder/expose -X POST -d "{\"local\":\":61001\",\"remote\":\"192.168.127.2:61001\"}"
rdctl shell curl http://192.168.127.1:80/services/forwarder/expose -X POST -d "{\"local\":\":61002\",\"remote\":\"192.168.127.2:61002\"}"