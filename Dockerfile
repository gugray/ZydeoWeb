FROM ubuntu:23.04

RUN apt-get update
RUN apt-get install -y default-libmysqlclient-dev
RUN apt-get install -y default-mysql-client unixodbc libpq5
RUN apt-get install -y build-essential

COPY ./targetapp/sphinx-2.2.11-release.tar.gz /sphinx/
WORKDIR /sphinx
RUN tar -xvzf sphinx-2.2.11-release.tar.gz
WORKDIR /sphinx/sphinx-2.2.11-release
RUN ./configure
RUN make
RUN make install

RUN apt-get install -y dotnet-sdk-6.0
RUN apt-get install -y libdbi-perl
RUN cpan URI::Escape
RUN cpan Time::HiRes
RUN apt-get install -y libdbd-mysql-perl
RUN apt-get install -y git
RUN apt-get install -y curl

WORKDIR /app
COPY ./targetapp/sphinx.pl /app/sphinx.pl
COPY ./targetapp/zhhu.conf /app/zhhu.conf
COPY ./targetapp/dropbox_uploader.sh /app/dropbox_uploader.sh
RUN ["chmod", "+x", "/app/dropbox_uploader.sh"]
COPY ./targetapp/startup.sh /app/startup.sh
RUN ["chmod", "+x", "/app/startup.sh"]
COPY ./_deploy_chsite /app/chsite/

ENV MUTATION=CHD
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT [ "bash", "-c", "/app/startup.sh" ]
