-- Migration 0004: Create the addresses table.
--
-- customer_id references customers(id) with CASCADE DELETE:
-- removing a customer also removes all of their addresses.
CREATE TABLE IF NOT EXISTS addresses
(
    id          UUID         NOT NULL DEFAULT gen_random_uuid(),
    customer_id UUID         NOT NULL,
    line1       VARCHAR(200) NOT NULL,
    line2       VARCHAR(200) NULL,
    city        VARCHAR(100) NOT NULL,
    postal_code VARCHAR(20)  NOT NULL,
    country     VARCHAR(100) NOT NULL,
    is_primary  BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT pk_addresses          PRIMARY KEY (id),
    CONSTRAINT fk_addresses_customer FOREIGN KEY (customer_id)
        REFERENCES customers (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_addresses_customer_id        ON addresses (customer_id);
CREATE INDEX IF NOT EXISTS ix_addresses_customer_is_primary ON addresses (customer_id, is_primary);
