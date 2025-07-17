FROM gcr.io/google.com/cloudsdktool/cloud-sdk:latest

RUN echo 'deb http://deb.debian.org/debian/ sid main' >> /etc/apt/sources.list && \
    apt-get update && apt-get -qqy upgrade && \
    apt-get -y -t sid install openjdk-21-jre-headless && \
    rm -rf /var/lib/apt/lists

EXPOSE 9001
ENV CLOUDSDK_CORE_PROJECT=local-dev

# Make sure the emulator can start
RUN bash -c 'gcloud beta emulators datastore start & GCLOUD_PID=$!; echo $GCLOUD_PID; sleep 1; ps -p $GCLOUD_PID && kill $GCLOUD_PID'

# DO NOT UNDER ANY CIRCUMSTANCES ADD THE --use-firestore-in-datastore-mode FLAG
#
# This flag does not properly emulate "Firestore in Datastore mode" and *actively* breaks
# transaction integrity in the emulator, leading to transaction commits going through
# when they should fail with contention errors.
#
ENTRYPOINT [ "gcloud", "beta", "emulators", "datastore", "start", "--host-port=0.0.0.0:9001", "--no-store-on-disk", "--consistency=1.0" ]