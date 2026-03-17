-- Create appdata database and enable pgvector extension.
-- Runs at container startup; Aspire's AddDatabase will skip create if already exists.
CREATE DATABASE appdata;

\connect appdata

CREATE EXTENSION IF NOT EXISTS vector;
