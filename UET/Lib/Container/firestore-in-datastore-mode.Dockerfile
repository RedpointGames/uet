FROM gcr.io/google.com/cloudsdktool/cloud-sdk:latest

RUN echo 'deb http://deb.debian.org/debian/ sid main' >> /etc/apt/sources.list && \
    apt-get update && apt-get -qqy upgrade && \
    apt-get -y -t sid install openjdk-21-jre-headless && \
    rm -rf /var/lib/apt/lists

EXPOSE 9001
ENV CLOUDSDK_CORE_PROJECT=local-dev

# Make sure the emulator can start
RUN bash -c 'gcloud emulators firestore start & GCLOUD_PID=$!; echo $GCLOUD_PID; sleep 1; ps -p $GCLOUD_PID && kill $GCLOUD_PID'

ENTRYPOINT [ "gcloud", "emulators", "firestore", "start", "--database-mode=datastore-mode", "--host-port=0.0.0.0:9001" ]