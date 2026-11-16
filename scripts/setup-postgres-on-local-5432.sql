-- Run as a PostgreSQL superuser (e.g. psql -U postgres -f setup-postgres-on-local-5432.sql)
-- Use this ONLY if you want to use the Windows PostgreSQL service on port 5432
-- instead of Docker on port 5433.

CREATE USER pettycash WITH PASSWORD 'pettycash_dev';
CREATE DATABASE pettycash OWNER pettycash;
GRANT ALL PRIVILEGES ON DATABASE pettycash TO pettycash;
