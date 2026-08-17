"""
Delete an employee by ID.

Usage:
    python delete_employee.py <employee_id>
    python delete_employee.py 3
"""
import sys
from app.database import SessionLocal
from app.models_db import Employee


def delete_employee(employee_id: int):
    db = SessionLocal()
    try:
        employee = db.query(Employee).filter(Employee.id == employee_id).first()

        if not employee:
            print(f"No employee found with id={employee_id}")
            return

        print(f"Found: id={employee.id} name={employee.name!r} code={employee.employee_code!r}")
        confirm = input("Delete this employee? (y/N): ").strip().lower()
        if confirm != "y":
            print("Cancelled.")
            return

        db.delete(employee)
        db.commit()
        print(f"Deleted employee id={employee_id}.")

    except Exception as e:
        db.rollback()
        print(f"Error deleting employee: {e}")
        # If this is a foreign-key violation (e.g. visit_logs.host_employee_id
        # references this employee), the delete will fail rather than silently
        # cascading. Decide deliberately whether you want ON DELETE SET NULL /
        # CASCADE on that FK, or whether blocking deletion here is the safer
        # default for an audit trail like visit_logs.
    finally:
        db.close()


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python delete_employee.py <employee_id>")
        sys.exit(1)

    try:
        emp_id = int(sys.argv[1])
    except ValueError:
        print("employee_id must be an integer")
        sys.exit(1)

    delete_employee(emp_id)