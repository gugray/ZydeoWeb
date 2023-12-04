FROM ubuntu:23.04

RUN apt-get update
RUN apt-get install -y dotnet-sdk-6.0
RUN apt-get install -y mysql-client
RUN apt-get install -y git
RUN apt-get install -y curl

WORKDIR /app
COPY ./targetapp/dropbox_uploader.sh /app/dropbox_uploader.sh
RUN ["chmod", "+x", "/app/dropbox_uploader.sh"]
COPY ./targetapp/startup.sh /app/startup.sh
RUN ["chmod", "+x", "/app/startup.sh"]
COPY ./_deploy_chsite /app/chsite/

ENV MUTATION=HDD
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT [ "bash", "-c", "/app/startup.sh" ]
