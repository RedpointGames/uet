FROM gcr.io/google.com/cloudsdktool/cloud-sdk:latest

EXPOSE 9001
ENV CLOUDSDK_CORE_PROJECT=local-dev

ENTRYPOINT [ "gcloud", "emulators", "firestore", "start", "--database-mode=datastore-mode", "--host-port=0.0.0.0:9001" ]