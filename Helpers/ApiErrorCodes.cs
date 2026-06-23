namespace PharmacyFinder.API.Helpers;

public static class ApiErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string BadRequest = "BAD_REQUEST";
    public const string InternalError = "INTERNAL_ERROR";

    public const string AuthInvalidEmail = "AUTH_INVALID_EMAIL";
    public const string AuthEmailExists = "AUTH_EMAIL_EXISTS";
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthCannotRegisterAdmin = "AUTH_CANNOT_REGISTER_ADMIN";
    public const string AuthInvalidToken = "AUTH_INVALID_TOKEN";

    public const string PharmacyNotFound = "PHARMACY_NOT_FOUND";
    public const string PharmacyForbidden = "PHARMACY_FORBIDDEN";

    public const string MedicineNotFound = "MEDICINE_NOT_FOUND";
    public const string StockNotFound = "STOCK_NOT_FOUND";
    public const string StockAlreadyExists = "STOCK_ALREADY_EXISTS";

    public const string PrescriptionNotFound = "PRESCRIPTION_NOT_FOUND";
    public const string PrescriptionForbidden = "PRESCRIPTION_FORBIDDEN";
    public const string PrescriptionNoMedicines = "PRESCRIPTION_NO_MEDICINES";
    public const string PrescriptionOcrFailed = "PRESCRIPTION_OCR_FAILED";
    public const string PrescriptionImageInvalid = "PRESCRIPTION_IMAGE_INVALID";
    public const string PrescriptionOcrSetup = "PRESCRIPTION_OCR_SETUP";

    public const string SearchInvalidQuery = "SEARCH_INVALID_QUERY";

    public const string UserNotFound = "USER_NOT_FOUND";

    public const string AdminLastAdmin = "ADMIN_LAST_ADMIN";
    public const string AdminCannotDemoteSelf = "ADMIN_CANNOT_DEMOTE_SELF";
}
