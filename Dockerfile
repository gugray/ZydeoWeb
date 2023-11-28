FROM ubuntu:23.04 as sphinx_build

RUN apt-get update
RUN apt-get install -y default-libmysqlclient-dev
RUN apt-get install -y default-mysql-client unixodbc libpq5
RUN apt-get install -y build-essential

WORKDIR /sphinx
COPY ./Sphinx-2.2.11 /sphinx/
RUN ./configure
RUN make
RUN make install

RUN apt-get install -y dotnet-sdk-6.0
RUN apt-get install -y libdbi-perl
RUN cpan URI::Escape
RUN cpan Time::HiRes
RUN apt-get install -y libdbd-mysql-perl

WORKDIR /app
COPY ./targetapp/sphinx.pl /app/sphinx.pl
COPY ./targetapp/zhhu.conf /app/zhhu.conf
COPY ./_deploy_chsite /app/chsite/
ENV MUTATION=CHD
ENV ASPNETCORE_ENVIRONMENT=Production

COPY ./targetapp/startup.sh /app/startup.sh
RUN ["chmod", "+x", "/app/startup.sh"]
ENTRYPOINT [ "bash", "-c", "/app/startup.sh" ]
