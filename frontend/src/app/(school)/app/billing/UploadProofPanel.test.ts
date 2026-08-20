import { describe, expect, test } from "bun:test";
import { buildPaymentProofUploadRequest } from "./UploadProofPanel";

describe("buildPaymentProofUploadRequest", () => {
  test("uses the backend UploadRequest field names", () => {
    const file = new File(["proof"], "payment-proof.pdf", {
      type: "application/pdf",
    });

    expect(buildPaymentProofUploadRequest(file)).toEqual({
      fileName: "payment-proof.pdf",
      contentType: "application/pdf",
      sizeBytes: 5,
    });
  });
});
