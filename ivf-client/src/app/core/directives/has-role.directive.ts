import { Directive, Input, TemplateRef, ViewContainerRef, inject, effect } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

@Directive({
    selector: '[appHasRole]',
    standalone: true
})
export class HasRoleDirective {
    private templateRef = inject(TemplateRef<any>);
    private viewContainer = inject(ViewContainerRef);
    private authService = inject(AuthService);

    private requiredRoles: string[] = [];
    private isHidden = true;

    constructor() {
        effect(() => {
            const user = this.authService.user();
            this.updateView(user?.role);
        });
    }

    @Input()
    set appHasRole(roles: string[] | string) {
        this.requiredRoles = Array.isArray(roles) ? roles : [roles];
        const user = this.authService.user();
        this.updateView(user?.role);
    }

    private updateView(userRole?: string) {
        if (!userRole) {
            this.viewContainer.clear();
            this.isHidden = true;
            return;
        }

        // "Admin" usually has access to everything, or check specific roles
        const hasRole = this.requiredRoles.includes(userRole) || userRole === 'Admin';

        if (hasRole && this.isHidden) {
            this.viewContainer.createEmbeddedView(this.templateRef);
            this.isHidden = false;
        } else if (!hasRole && !this.isHidden) {
            this.viewContainer.clear();
            this.isHidden = true;
        }
    }
}
