namespace Staybnb.Models;

public enum ApplicationStatus { Pending, Approved, Rejected }
public enum BookingStatus { Pending, Approved, Rejected, CheckedIn, Completed, Cancelled }
public enum PaymentStatus { Pending, Paid, Refunded }
public enum ActivityType { Login, Logout, Registration, RoleChange, Application, Booking, Property, CheckIn, Message, System }
public enum NotificationType { Info, Success, Warning, Booking, Message, Application, CheckIn }
public enum CheckInStatus { InProgress, Submitted, Verified, Rejected }
public enum DocumentStatus { Pending, Verified, Rejected }
public enum PropertyType { Apartment, House, Villa, Cabin, Cottage, Guesthouse }
