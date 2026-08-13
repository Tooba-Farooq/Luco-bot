"""add available_again_at to visit_sessions

Revision ID: 624f24b7ff64
Revises: d873d0d18d9b
Create Date: 2026-08-03 08:07:50.341685

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = '624f24b7ff64'
down_revision: Union[str, Sequence[str], None] = 'd873d0d18d9b'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    # NOTE: the create_unique_constraint calls for employee_code and
    # invite_token were removed here — both are already declared unique
    # in the initial schema migration (c7d611282b50), so re-adding them
    # is a duplicate. This only worked silently on SQLite because batch
    # mode there rebuilds the whole table instead of issuing a direct
    # ALTER TABLE ADD CONSTRAINT.
    with op.batch_alter_table('visit_sessions', schema=None) as batch_op:
        batch_op.add_column(sa.Column('available_again_at', sa.DateTime(), nullable=True))


def downgrade() -> None:
    """Downgrade schema."""
    with op.batch_alter_table('visit_sessions', schema=None) as batch_op:
        batch_op.drop_column('available_again_at')