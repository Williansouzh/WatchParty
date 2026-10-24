import 'package:flutter/material.dart';

import '../core/theme.dart';

/// The WatchParty mark: two play buttons, you in front and your friends' echo behind.
/// Drawn from the same geometry as branding/watchparty-mark.svg.
class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.size = 40});

  final double size;

  @override
  Widget build(BuildContext context) =>
      CustomPaint(size: Size.square(size), painter: const _MarkPainter());
}

/// Mark + lowercase "watchparty" wordmark.
class BrandLogo extends StatelessWidget {
  const BrandLogo({super.key, this.size = 40});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'WatchParty',
      child: ExcludeSemantics(
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            BrandMark(size: size),
            SizedBox(width: size * 0.25),
            Text(
              'watchparty',
              style: TextStyle(fontSize: size * 0.68, fontWeight: FontWeight.w900, letterSpacing: -size * 0.03, height: 1),
            ),
          ],
        ),
      ),
    );
  }
}

class _MarkPainter extends CustomPainter {
  const _MarkPainter();

  static const _echo = Color(0xFF0E7A3A);

  @override
  void paint(Canvas canvas, Size size) {
    canvas.scale(size.width / 120);
    canvas.drawCircle(const Offset(60, 60), 60, Paint()..color = AppColors.accent);
    _triangle(canvas, 31, _echo);
    _triangle(canvas, 46, Colors.black);
  }

  void _triangle(Canvas canvas, double left, Color color) {
    final path = Path()
      ..moveTo(left, 35)
      ..lineTo(left, 85)
      ..lineTo(left + 43, 60)
      ..close();
    canvas.drawPath(
      path,
      Paint()
        ..color = color
        ..style = PaintingStyle.fill,
    );
    canvas.drawPath(
      path,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = 10
        ..strokeJoin = StrokeJoin.round,
    );
  }

  @override
  bool shouldRepaint(_MarkPainter oldDelegate) => false;
}
