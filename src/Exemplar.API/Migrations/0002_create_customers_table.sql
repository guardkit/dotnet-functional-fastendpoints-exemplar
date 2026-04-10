-- Migration 0002: Create the customers table.
--
-- status is stored as an integer matching the CustomerStatus enum:
--   0 = Active, 1 = Inactive
-- Dapper maps the integer column directly to the enum via implicit cast.
CREATE TABLE IF NOT EXISTS customers
(
    id         UUID        NOT NULL DEFAULT gen_random_uuid(),
    name       VARCHAR(200) NOT NULL,
    email      VARCHAR(320) NOT NULL,
    status     INTEGER      NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT pk_customers         PRIMARY KEY (id),
    CONSTRAINT uq_customers_email   UNIQUE (email),
    CONSTRAINT chk_customers_status CHECK (status IN (0, 1))
);

CREATE INDEX IF NOT EXISTS ix_customers_email  ON customers (email);
CREATE INDEX IF NOT EXISTS ix_customers_status ON customers (status);
