--liquibase formatted sql

--changeset student:010-create-lab-orders-table
-- Test entity for laboratory work №1. Deliberately has no secondary indexes:
-- they are created and measured in the laboratory script.
CREATE TABLE "LabOrders" (
    "Id" BIGSERIAL PRIMARY KEY,
    "CustomerId" INTEGER NOT NULL,
    "Status" VARCHAR(20) NOT NULL,
    "TotalAmount" DECIMAL(12,2) NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL
);
