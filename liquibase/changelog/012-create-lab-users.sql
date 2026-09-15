--liquibase formatted sql

--changeset student:012-create-lab-users
CREATE TABLE users (
    id BIGSERIAL PRIMARY KEY,
    email VARCHAR(255) NOT NULL
);
